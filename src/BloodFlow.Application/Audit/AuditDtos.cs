using BloodFlow.Application.Common;
using BloodFlow.Domain.Enums;

namespace BloodFlow.Application.Audit
{
    public sealed record AuditLogQuery : PageQuery
    {
        public string? EntityType { get; init; }
        public string? EntityId { get; init; }
        public int? ActorUserId { get; init; }
        public string? Action { get; init; }
        public DateTime? FromUtc { get; init; }
        public DateTime? ToUtc { get; init; }
    }

    public sealed record AuditLogDto(
        long Id,
        DateTime OccurredAt,
        ActorType ActorType,
        int? ActorUserId,
        string? ActorRole,
        int? OrganizationId,
        string Action,
        string EntityType,
        string? EntityId,
        string? OldValues,
        string? NewValues,
        string? Reason,
        string? IpAddress);
}
