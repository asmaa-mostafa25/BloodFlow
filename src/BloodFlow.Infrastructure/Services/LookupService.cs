using BloodFlow.Application.Lookups;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Services
{
    internal class LookupService : ILookupService
    {
        private readonly AppDbContext _db;

        public LookupService(AppDbContext db) => _db = db;

        public async Task<IReadOnlyList<GovernorateDto>> GetGovernoratesAsync(CancellationToken cancellationToken = default) =>
            await _db.Governorates.AsNoTracking()
                .OrderBy(g => g.Id)
                .Select(g => new GovernorateDto(g.Id, g.Name))
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<BloodGroupDto>> GetBloodGroupsAsync(CancellationToken cancellationToken = default) =>
            await _db.BloodGroups.AsNoTracking()
                .OrderBy(b => b.Id)
                .Select(b => new BloodGroupDto(b.Id, b.Code))
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<OrganizationLookupDto>> GetActiveOrganizationsAsync(
            OrganizationType? type, short? governorateId, CancellationToken cancellationToken = default)
        {
            var organizations = _db.Organizations.AsNoTracking()
                .Where(o => o.Status == OrganizationStatus.Active);

            if (type.HasValue)
                organizations = organizations.Where(o => o.Type == type.Value);
            if (governorateId.HasValue)
                organizations = organizations.Where(o => o.GovernorateId == governorateId.Value);

            return await organizations
                .OrderBy(o => o.Name)
                .Select(o => new OrganizationLookupDto(o.Id, o.Name, o.Type, o.GovernorateId, o.CityName))
                .ToListAsync(cancellationToken);
        }
    }
}
