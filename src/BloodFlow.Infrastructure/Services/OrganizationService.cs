using BloodFlow.Application.Common;
using BloodFlow.Application.Organizations;
using BloodFlow.Domain.Entities;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Common;
using BloodFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Services
{
    internal class OrganizationService : IOrganizationService
    {
        private readonly AppDbContext _db;
        private readonly AuditLogger _audit;

        public OrganizationService(AppDbContext db, AuditLogger audit)
        {
            _db = db;
            _audit = audit;
        }

        public async Task<PagedResult<OrganizationDto>> GetAsync(OrganizationQuery query, CancellationToken cancellationToken = default)
        {
            var organizations = _db.Organizations.AsNoTracking();

            if (query.Type.HasValue)
                organizations = organizations.Where(o => o.Type == query.Type.Value);
            if (query.Status.HasValue)
                organizations = organizations.Where(o => o.Status == query.Status.Value);
            if (query.GovernorateId.HasValue)
                organizations = organizations.Where(o => o.GovernorateId == query.GovernorateId.Value);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                organizations = organizations.Where(o => o.Name.Contains(term) || o.CityName.Contains(term));
            }

            var total = await organizations.CountAsync(cancellationToken);

            var rows = await Project(organizations.OrderBy(o => o.Name).ThenBy(o => o.Id))
                .Skip(query.Skip)
                .Take(query.SafePageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<OrganizationDto>(rows.Select(ToDto).ToList(), query.SafePage, query.SafePageSize, total);
        }

        public async Task<Result<OrganizationDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var row = await Project(_db.Organizations.AsNoTracking().Where(o => o.Id == id))
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
            {
                return NotFound(id);
            }
            return ToDto(row);
        }

        public async Task<Result<OrganizationDto>> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default)
        {
            var name = request.Name.Trim();
            var city = request.CityName.Trim();

            var governorateError = await CheckGovernorateAsync(request.GovernorateId, cancellationToken);
            if (governorateError is not null) return governorateError;

            if (await NameTakenAsync(name, request.GovernorateId, excludeId: null, cancellationToken))
            {
                return DuplicateName();
            }

            var organization = new Organization
            {
                Type = request.Type!.Value,
                Name = name,
                CityName = city,
                GovernorateId = request.GovernorateId,
                Status = OrganizationStatus.Active
            };

            // The audit row needs the new Id, which exists only after the first save.
            // A transaction keeps the organization and its audit row all-or-nothing.
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            _db.Organizations.Add(organization);
            await _db.SaveChangesAsync(cancellationToken);

            _audit.Add(AuditActions.OrganizationCreated, AuditEntityTypes.Organization, organization.Id.ToString(),
                newValues: Snapshot(organization),
                organizationId: organization.Id);
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await GetByIdAsync(organization.Id, cancellationToken);
        }

        public async Task<Result<OrganizationDto>> UpdateAsync(int id, UpdateOrganizationRequest request, CancellationToken cancellationToken = default)
        {
            if (!RowVersionConverter.TryParse(request.RowVersion, out var rowVersion))
            {
                return Error.Validation("Organization.InvalidRowVersion", "RowVersion is missing or invalid.");
            }

            var organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
            if (organization is null) return NotFound(id);

            var name = request.Name.Trim();
            var city = request.CityName.Trim();

            var governorateError = await CheckGovernorateAsync(request.GovernorateId, cancellationToken);
            if (governorateError is not null) return governorateError;

            if (await NameTakenAsync(name, request.GovernorateId, excludeId: id, cancellationToken))
            {
                return DuplicateName();
            }

            // Tell EF which version the client saw. If the row changed since then,
            // SaveChanges throws DbUpdateConcurrencyException instead of overwriting.
            _db.Entry(organization).Property(o => o.RowVersion).OriginalValue = rowVersion;

            var before = Snapshot(organization);
            organization.Name = name;
            organization.CityName = city;
            organization.GovernorateId = request.GovernorateId;

            _audit.Add(AuditActions.OrganizationUpdated, AuditEntityTypes.Organization, id.ToString(),
                oldValues: before,
                newValues: Snapshot(organization),
                organizationId: id);

            var saveError = await SaveAsync(cancellationToken);
            if (saveError is not null) return saveError;

            return await GetByIdAsync(id, cancellationToken);
        }

        public async Task<Result> DeactivateAsync(int id, string reason, CancellationToken cancellationToken = default)
        {
            var organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
            if (organization is null) return NotFound(id);

            if (organization.Status != OrganizationStatus.Active)
            {
                return Error.Conflict("Organization.InvalidStatus", "Only active organizations can be deactivated.");
            }

            // Workflow A2: an organization with active reservations or open requests cannot be
            // deactivated (the blood would be stuck). Transfers are added to this check in Slice 6.
            var hasOpenWork =
                await _db.Reservations.AnyAsync(r => r.OrganizationId == id && r.Status == ReservationStatus.Active, cancellationToken)
                || await _db.BloodRequests.AnyAsync(r => r.DestinationHospitalId == id
                    && (r.Status == RequestStatus.Pending || r.Status == RequestStatus.Approved || r.Status == RequestStatus.Fulfilled),
                    cancellationToken);
            if (hasOpenWork)
            {
                return Error.Conflict("Organization.HasOpenWork",
                    "This organization has active reservations or open requests. Finish or cancel them first.");
            }

            organization.Status = OrganizationStatus.Deactivated;

            _audit.Add(AuditActions.OrganizationDeactivated, AuditEntityTypes.Organization, id.ToString(),
                oldValues: new { Status = OrganizationStatus.Active },
                newValues: new { organization.Status },
                reason: reason.Trim(),
                organizationId: id);

            var saveError = await SaveAsync(cancellationToken);
            return saveError is null ? Result.Success() : saveError;
        }

        public async Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default)
        {
            var organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
            if (organization is null) return NotFound(id);

            if (organization.Status != OrganizationStatus.Deactivated)
            {
                return Error.Conflict("Organization.InvalidStatus", "Only deactivated organizations can be activated.");
            }

            organization.Status = OrganizationStatus.Active;

            _audit.Add(AuditActions.OrganizationActivated, AuditEntityTypes.Organization, id.ToString(),
                oldValues: new { Status = OrganizationStatus.Deactivated },
                newValues: new { organization.Status },
                organizationId: id);

            var saveError = await SaveAsync(cancellationToken);
            return saveError is null ? Result.Success() : saveError;
        }

        // ---------- helpers ----------

        private sealed record OrganizationRow(
            int Id, OrganizationType Type, string Name, string CityName, short GovernorateId,
            string GovernorateName, OrganizationStatus Status, int ActiveStaffCount, byte[] RowVersion);

        private IQueryable<OrganizationRow> Project(IQueryable<Organization> organizations) =>
            organizations.Select(o => new OrganizationRow(
                o.Id,
                o.Type,
                o.Name,
                o.CityName,
                o.GovernorateId,
                o.Governorate.Name,
                o.Status,
                _db.StaffProfiles.Count(s => s.OrganizationId == o.Id
                    && _db.Users.Any(u => u.Id == s.UserId && u.AccountStatus == AccountStatus.Active)),
                o.RowVersion));

        private static OrganizationDto ToDto(OrganizationRow r) => new(
            r.Id, r.Type, r.Name, r.CityName, r.GovernorateId, r.GovernorateName,
            r.Status, r.ActiveStaffCount, RowVersionConverter.ToBase64(r.RowVersion));

        private static object Snapshot(Organization o) =>
            new { o.Type, o.Name, o.CityName, o.GovernorateId, o.Status };

        private async Task<Error?> CheckGovernorateAsync(short governorateId, CancellationToken cancellationToken) =>
            await _db.Governorates.AnyAsync(g => g.Id == governorateId, cancellationToken)
                ? null
                : Error.Validation("Organization.GovernorateNotFound", $"Governorate {governorateId} does not exist.");

        private Task<bool> NameTakenAsync(string name, short governorateId, int? excludeId, CancellationToken cancellationToken) =>
            _db.Organizations.AnyAsync(o =>
                o.Name == name && o.GovernorateId == governorateId && (excludeId == null || o.Id != excludeId),
                cancellationToken);

        private async Task<Error?> SaveAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return null;
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.Conflict("Organization.Concurrency",
                    "This organization was changed by someone else. Reload and try again.");
            }
        }

        private static Error NotFound(int id) =>
            Error.NotFound("Organization.NotFound", $"Organization {id} was not found.");

        private static Error DuplicateName() =>
            Error.Conflict("Organization.DuplicateName", "An organization with this name already exists in this governorate.");
    }
}
