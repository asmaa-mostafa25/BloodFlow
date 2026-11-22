using BloodFlow.Application.Common;
using BloodFlow.Application.Staff;
using BloodFlow.Domain.Entities;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Common;
using BloodFlow.Infrastructure.Identity;
using BloodFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Services
{
    internal class StaffAdminService : IStaffAdminService
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditLogger _audit;
        private readonly ICurrentUser _currentUser;

        public StaffAdminService(AppDbContext db, UserManager<ApplicationUser> userManager, AuditLogger audit, ICurrentUser currentUser)
        {
            _db = db;
            _userManager = userManager;
            _audit = audit;
            _currentUser = currentUser;
        }

        public async Task<PagedResult<StaffListItemDto>> GetStaffAsync(StaffQuery query, CancellationToken cancellationToken = default)
        {
            var staff =
                from s in _db.StaffProfiles.AsNoTracking()
                join u in _db.Users.AsNoTracking() on s.UserId equals u.Id
                select new { Profile = s, User = u };

            if (query.Status.HasValue)
            {
                staff = staff.Where(x => x.User.AccountStatus == query.Status.Value);
            }
            if (query.OrganizationId.HasValue)
            {
                staff = staff.Where(x => x.Profile.OrganizationId == query.OrganizationId.Value);
            }
            if (query.StaffKind.HasValue)
            {
                staff = staff.Where(x => x.Profile.StaffKind == query.StaffKind.Value);
            }
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                staff = staff.Where(x => x.User.FullName.Contains(term) || x.User.Email!.Contains(term));
            }

            var total = await staff.CountAsync(cancellationToken);

            // The Pending list is a queue: oldest request first. Other lists: newest first.
            staff = query.Status == AccountStatus.Pending
                ? staff.OrderBy(x => x.User.CreatedAt).ThenBy(x => x.User.Id)
                : staff.OrderByDescending(x => x.User.CreatedAt).ThenByDescending(x => x.User.Id);

            var items = await staff
                .Skip(query.Skip)
                .Take(query.SafePageSize)
                .Select(x => new StaffListItemDto(
                    x.User.Id,
                    x.User.FullName,
                    x.User.Email!,
                    x.Profile.JobTitle,
                    x.Profile.StaffKind,
                    x.Profile.OrganizationId,
                    x.Profile.Organization.Name,
                    x.Profile.Organization.Governorate.Name,
                    x.User.AccountStatus,
                    x.User.CreatedAt))
                .ToListAsync(cancellationToken);

            return new PagedResult<StaffListItemDto>(items, query.SafePage, query.SafePageSize, total);
        }

        public async Task<Result<StaffDetailsDto>> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            var dto = await (
                from s in _db.StaffProfiles.AsNoTracking()
                join u in _db.Users.AsNoTracking() on s.UserId equals u.Id
                where s.UserId == userId
                select new StaffDetailsDto(
                    u.Id,
                    u.FullName,
                    u.Email!,
                    s.JobTitle,
                    s.StaffKind,
                    s.OrganizationId,
                    s.Organization.Name,
                    s.Organization.Status,
                    s.Organization.Governorate.Name,
                    u.AccountStatus,
                    u.CreatedAt,
                    s.ApprovedByUserId,
                    s.ApprovedAt,
                    s.RejectionReason))
                .FirstOrDefaultAsync(cancellationToken);

            if (dto is null)
            {
                return StaffNotFound(userId);
            }
            return dto;
        }

        public async Task<Result> ApproveAsync(int userId, CancellationToken cancellationToken = default)
        {
            var (user, profile, error) = await LoadAsync(userId, cancellationToken);
            if (error is not null) return error;

            if (user!.AccountStatus != AccountStatus.Pending)
            {
                return InvalidTransition("approved", user.AccountStatus, AccountStatus.Pending);
            }
            if (profile!.Organization.Status != OrganizationStatus.Active)
            {
                return OrganizationInactive();
            }

            var old = user.AccountStatus;
            user.AccountStatus = AccountStatus.Active;
            profile.ApprovedByUserId = _currentUser.UserId;
            profile.ApprovedAt = DateTime.UtcNow;
            profile.RejectionReason = null;

            _audit.Add(AuditActions.StaffApproved, AuditEntityTypes.User, user.Id.ToString(),
                oldValues: new { AccountStatus = old },
                newValues: new { user.AccountStatus, profile.ApprovedByUserId, profile.ApprovedAt },
                organizationId: profile.OrganizationId);

            return await SaveUserAsync(user);
        }

        public async Task<Result> RejectAsync(int userId, string reason, CancellationToken cancellationToken = default)
        {
            var (user, profile, error) = await LoadAsync(userId, cancellationToken);
            if (error is not null) return error;

            if (user!.AccountStatus != AccountStatus.Pending)
            {
                return InvalidTransition("rejected", user.AccountStatus, AccountStatus.Pending);
            }

            var old = user.AccountStatus;
            user.AccountStatus = AccountStatus.Rejected;
            profile!.RejectionReason = reason.Trim();

            _audit.Add(AuditActions.StaffRejected, AuditEntityTypes.User, user.Id.ToString(),
                oldValues: new { AccountStatus = old },
                newValues: new { user.AccountStatus },
                reason: profile.RejectionReason,
                organizationId: profile.OrganizationId);

            return await SaveUserAsync(user);
        }

        public async Task<Result> DeactivateAsync(int userId, string reason, CancellationToken cancellationToken = default)
        {
            var (user, profile, error) = await LoadAsync(userId, cancellationToken);
            if (error is not null) return error;

            if (user!.AccountStatus != AccountStatus.Active)
            {
                return InvalidTransition("deactivated", user.AccountStatus, AccountStatus.Active);
            }

            var old = user.AccountStatus;
            user.AccountStatus = AccountStatus.Deactivated;
            // Changing the security stamp invalidates anything tied to the old one
            // (e.g. password reset links). Live JWTs are blocked by the status check on every request.
            user.SecurityStamp = Guid.NewGuid().ToString();

            _audit.Add(AuditActions.StaffDeactivated, AuditEntityTypes.User, user.Id.ToString(),
                oldValues: new { AccountStatus = old },
                newValues: new { user.AccountStatus },
                reason: reason.Trim(),
                organizationId: profile!.OrganizationId);

            return await SaveUserAsync(user);
        }

        public async Task<Result> ReactivateAsync(int userId, CancellationToken cancellationToken = default)
        {
            var (user, profile, error) = await LoadAsync(userId, cancellationToken);
            if (error is not null) return error;

            if (user!.AccountStatus != AccountStatus.Deactivated)
            {
                return InvalidTransition("reactivated", user.AccountStatus, AccountStatus.Deactivated);
            }
            if (profile!.Organization.Status != OrganizationStatus.Active)
            {
                return OrganizationInactive();
            }

            var old = user.AccountStatus;
            user.AccountStatus = AccountStatus.Active;

            _audit.Add(AuditActions.StaffReactivated, AuditEntityTypes.User, user.Id.ToString(),
                oldValues: new { AccountStatus = old },
                newValues: new { user.AccountStatus },
                organizationId: profile.OrganizationId);

            return await SaveUserAsync(user);
        }

        // ---------- helpers ----------

        private async Task<(ApplicationUser? User, StaffProfile? Profile, Error? Error)> LoadAsync(
            int userId, CancellationToken cancellationToken)
        {
            var profile = await _db.StaffProfiles
                .Include(s => s.Organization)
                .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

            var user = profile is null ? null : await _userManager.FindByIdAsync(userId.ToString());

            return user is null || profile is null
                ? (null, null, StaffNotFound(userId))
                : (user, profile, null);
        }

        /// <summary>
        /// UserManager.UpdateAsync calls SaveChanges on the shared DbContext, so the user,
        /// the staff profile and the audit row are all saved together in one transaction.
        /// Identity also checks the user's ConcurrencyStamp: if two admins act on the same
        /// account at the same time, the second one gets a conflict instead of overwriting.
        /// </summary>
        private async Task<Result> SaveUserAsync(ApplicationUser user)
        {
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                return Result.Success();
            }

            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
            {
                return Error.Conflict("Staff.Concurrency", "This account was changed by someone else. Reload and try again.");
            }

            throw new InvalidOperationException(
                $"Failed to update user {user.Id}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        private static Error StaffNotFound(int userId) =>
            Error.NotFound("Staff.NotFound", $"Staff account {userId} was not found.");

        private static Error OrganizationInactive() =>
            Error.Conflict("Staff.OrganizationInactive", "The staff member's organization is not active.");

        private static Error InvalidTransition(string verb, AccountStatus current, AccountStatus required) =>
            Error.Conflict("Staff.InvalidStatus",
                $"Only {required} accounts can be {verb}. This account is {current}.");
    }
}
