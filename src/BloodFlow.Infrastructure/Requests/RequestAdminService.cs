using BloodFlow.Application.Common;
using BloodFlow.Application.Requests;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Common;
using BloodFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Requests
{
    /// <summary>
    /// Workflow A3: the System Admin monitors every request (overdue, unconfirmed, escalations)
    /// and reviews emergency overrides. The admin never sees patient names.
    /// </summary>
    internal sealed class RequestAdminService : IRequestAdminService
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _currentUser;
        private readonly IClock _clock;
        private readonly AuditLogger _audit;
        private readonly RequestReads _reads;

        public RequestAdminService(AppDbContext db, ICurrentUser currentUser, IClock clock, AuditLogger audit, RequestReads reads)
        {
            _db = db;
            _currentUser = currentUser;
            _clock = clock;
            _audit = audit;
            _reads = reads;
        }

        public async Task<PagedResult<RequestSummaryDto>> GetAllAsync(RequestQuery query, CancellationToken cancellationToken = default)
        {
            var flags = await _reads.GetFlagContextAsync(cancellationToken);
            var requests = RequestReads.ApplyFilters(_db.BloodRequests.AsNoTracking(), query);
            if (query.OnlyFlagged) requests = flags.OnlyFlagged(requests);

            return await _reads.PageAsync(requests, query, flags, cancellationToken);
        }

        public async Task<PagedResult<EmergencyOverrideDto>> GetOverridesAsync(OverrideQuery query, CancellationToken cancellationToken = default)
        {
            var overrides = _db.EmergencyOverrides.AsNoTracking();
            if (query.ReviewStatus.HasValue) overrides = overrides.Where(o => o.ReviewStatus == query.ReviewStatus.Value);

            var total = await overrides.CountAsync(cancellationToken);
            var items = await overrides
                .OrderByDescending(o => o.OverriddenAt)
                .Skip(query.Skip)
                .Take(query.SafePageSize)
                .Select(o => new EmergencyOverrideDto(
                    o.Id, o.RequestId, o.Request.RequestNumber,
                    o.OrganizationId, _db.Organizations.Where(x => x.Id == o.OrganizationId).Select(x => x.Name).First(),
                    o.OverriddenByUserId, o.ReasonText, o.OverriddenAt,
                    o.ReviewStatus, o.ReviewedByUserId, o.ReviewedAt, o.ReviewNotes))
                .ToListAsync(cancellationToken);

            return new PagedResult<EmergencyOverrideDto>(items, query.SafePage, query.SafePageSize, total);
        }

        public async Task<Result> ReviewOverrideAsync(int overrideId, string? notes, CancellationToken cancellationToken = default)
        {
            var entity = await _db.EmergencyOverrides.FirstOrDefaultAsync(o => o.Id == overrideId, cancellationToken);
            if (entity is null)
            {
                return Error.NotFound("EmergencyOverride.NotFound", $"Emergency override {overrideId} was not found.");
            }
            if (entity.ReviewStatus == OverrideReviewStatus.Reviewed)
            {
                return Error.Conflict("EmergencyOverride.AlreadyReviewed", "This override was already reviewed.");
            }

            entity.ReviewStatus = OverrideReviewStatus.Reviewed;
            entity.ReviewedByUserId = _currentUser.UserId;
            entity.ReviewedAt = _clock.UtcNow;
            entity.ReviewNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

            _audit.Add(AuditActions.EmergencyOverrideReviewed, AuditEntityTypes.EmergencyOverride, entity.Id.ToString(),
                oldValues: new { ReviewStatus = OverrideReviewStatus.PendingReview },
                newValues: new { entity.ReviewStatus, entity.RequestId },
                reason: entity.ReviewNotes,
                organizationId: entity.OrganizationId);

            await _db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
