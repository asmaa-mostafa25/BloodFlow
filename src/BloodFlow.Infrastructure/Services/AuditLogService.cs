using BloodFlow.Application.Audit;
using BloodFlow.Application.Common;
using BloodFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Services
{
    internal class AuditLogService : IAuditLogService
    {
        private readonly AppDbContext _db;

        public AuditLogService(AppDbContext db) => _db = db;

        public async Task<PagedResult<AuditLogDto>> GetAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
        {
            var logs = _db.AuditLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query.EntityType))
                logs = logs.Where(a => a.EntityType == query.EntityType);
            if (!string.IsNullOrWhiteSpace(query.EntityId))
                logs = logs.Where(a => a.EntityId == query.EntityId);
            if (query.ActorUserId.HasValue)
                logs = logs.Where(a => a.ActorUserId == query.ActorUserId);
            if (!string.IsNullOrWhiteSpace(query.Action))
                logs = logs.Where(a => a.Action == query.Action);
            if (query.FromUtc.HasValue)
                logs = logs.Where(a => a.OccurredAt >= query.FromUtc.Value);
            if (query.ToUtc.HasValue)
                logs = logs.Where(a => a.OccurredAt < query.ToUtc.Value);

            var total = await logs.CountAsync(cancellationToken);

            var items = await logs
                .OrderByDescending(a => a.Id)
                .Skip(query.Skip)
                .Take(query.SafePageSize)
                .Select(a => new AuditLogDto(
                    a.Id, a.OccurredAt, a.ActorType, a.ActorUserId, a.ActorRole, a.OrganizationId,
                    a.Action, a.EntityType, a.EntityId, a.OldValues, a.NewValues, a.Reason, a.IpAddress))
                .ToListAsync(cancellationToken);

            return new PagedResult<AuditLogDto>(items, query.SafePage, query.SafePageSize, total);
        }
    }
}
