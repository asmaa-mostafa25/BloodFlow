using BloodFlow.Domain.Enums;

namespace BloodFlow.Domain.Entities
{
    /// <summary>
    /// A group of blood units of one blood group, at one organization, with one expiry date.
    /// Available = OnHand − Reserved (and only while the batch is Active).
    /// Quantities only change through inventory transactions (the ledger), never silently.
    /// </summary>
    public class BloodBatch
    {
        public int Id { get; set; }

        public int OrganizationId { get; set; }
        public Organization Organization { get; set; } = null!;

        public byte BloodGroupId { get; set; }
        public BloodGroup BloodGroup { get; set; } = null!;

        public BatchSourceType SourceType { get; set; }

        /// <summary>Date the blood was collected from the donor. Kept unchanged even after transfers.</summary>
        public DateOnly OriginalCollectionDate { get; set; }

        /// <summary>The batch is usable until the end of this day (Cairo time).</summary>
        public DateOnly ExpiryDate { get; set; }
        public ExpiryBasis ExpiryBasis { get; set; }

        public int InitialQuantity { get; set; }
        public int OnHandQuantity { get; set; }
        public int ReservedQuantity { get; set; }

        public BatchStatus Status { get; set; } = BatchStatus.Active;
        public DateTime? ExpiredAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public int CreatedByUserId { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();

        /// <summary>Units that can still be reserved.</summary>
        public int AvailableQuantity => Status == BatchStatus.Active ? OnHandQuantity - ReservedQuantity : 0;
    }
}
