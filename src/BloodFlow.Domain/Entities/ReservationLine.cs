using BloodFlow.Domain.Enums;

namespace BloodFlow.Domain.Entities
{
    /// <summary>
    /// "N units of THIS batch are held for THIS reservation."
    /// Composite FKs make sure the batch belongs to the same organization and has the same
    /// blood group as the reservation (exact blood group only in the MVP).
    /// </summary>
    public class ReservationLine
    {
        public int Id { get; set; }

        public int ReservationId { get; set; }
        public Reservation Reservation { get; set; } = null!;

        public int BatchId { get; set; }
        public BloodBatch Batch { get; set; } = null!;

        public int OrganizationId { get; set; }
        public byte BloodGroupId { get; set; }

        public int Quantity { get; set; }

        public ReservationLineStatus Status { get; set; } = ReservationLineStatus.Active;

        /// <summary>Why the line became Invalidated (e.g. BatchExpired).</summary>
        public string? InvalidationReason { get; set; }
        public DateTime? ClosedAt { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
