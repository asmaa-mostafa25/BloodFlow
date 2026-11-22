using BloodFlow.Application.Common;

namespace BloodFlow.Application.Audit
{
    /// <summary>Read-only access to the audit trail for the System Admin.</summary>
    public interface IAuditLogService
    {
        Task<PagedResult<AuditLogDto>> GetAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
    }
}
