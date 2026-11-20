using BloodFlow.Domain.Enums;

namespace BloodFlow.Domain.Entities
{
    /// <summary>
    /// Append-only record of an important action: who did what, to which record,
    /// old value → new value, and why. Rows are never updated or deleted.
    /// </summary>
    public class AuditLog
    {
        public long Id { get; set; }
        public DateTime OccurredAt { get; set; }

        public ActorType ActorType { get; set; }
        public int? ActorUserId { get; set; }
        public string? ActorRole { get; set; }
        public int? OrganizationId { get; set; }

        public required string Action { get; set; }
        public required string EntityType { get; set; }
        public string? EntityId { get; set; }

        /// <summary>JSON snapshot before the change (sensitive fields masked).</summary>
        public string? OldValues { get; set; }
        /// <summary>JSON snapshot after the change (sensitive fields masked).</summary>
        public string? NewValues { get; set; }

        public string? Reason { get; set; }
        public Guid? CorrelationId { get; set; }
        public string? IpAddress { get; set; }
    }
}
