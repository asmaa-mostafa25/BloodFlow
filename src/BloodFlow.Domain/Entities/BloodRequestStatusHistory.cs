using BloodFlow.Domain.Enums;

namespace BloodFlow.Domain.Entities
{
    /// <summary>Immutable row for every status change of a request: who, when, from → to, why.</summary>
    public class BloodRequestStatusHistory
    {
        public long Id { get; set; }

        public int RequestId { get; set; }
        public BloodRequest Request { get; set; } = null!;

        /// <summary>Null for the first row (creation).</summary>
        public RequestStatus? FromStatus { get; set; }
        public RequestStatus ToStatus { get; set; }

        public int ChangedByUserId { get; set; }
        public string? Reason { get; set; }
        public DateTime ChangedAt { get; set; }
        public Guid? CorrelationId { get; set; }
    }
}
