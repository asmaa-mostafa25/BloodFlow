using BloodFlow.Domain.Enums;

namespace BloodFlow.Domain.Entities
{
    /// <summary>
    /// Units HELD for one request at one organization (the executing facility).
    /// The actual units come from specific batches, one <see cref="ReservationLine"/> per batch.
    /// A request has at most one Active-or-Consumed reservation.
    /// </summary>
    public class Reservation
    {
        public int Id { get; set; }

        public int RequestId { get; set; }
        public BloodRequest Request { get; set; } = null!;

        /// <summary>Same as the request's blood group (enforced by a composite FK).</summary>
        public byte BloodGroupId { get; set; }

        /// <summary>The organization that holds the units and will fulfil the request.</summary>
        public int OrganizationId { get; set; }
        public Organization Organization { get; set; } = null!;

        public ReservationStatus Status { get; set; } = ReservationStatus.Active;

        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ICollection<ReservationLine> Lines { get; set; } = new List<ReservationLine>();
    }
}
