using BloodFlow.Domain.Enums;

namespace BloodFlow.Domain.Entities
{
    /// <summary>
    /// One line in the append-only stock ledger. Every change to a batch quantity writes
    /// exactly one row here, with the deltas and the quantities right after the change.
    /// Rows are never updated or deleted: mistakes are fixed with a new Correction row.
    /// </summary>
    public class InventoryTransaction
    {
        public long Id { get; set; }

        public int BatchId { get; set; }
        public BloodBatch Batch { get; set; } = null!;

        /// <summary>Copied from the batch so the ledger can be filtered by organization fast.</summary>
        public int OrganizationId { get; set; }

        public InventoryTransactionType TransactionType { get; set; }

        public int OnHandDelta { get; set; }
        public int ReservedDelta { get; set; }
        public int OnHandAfter { get; set; }
        public int ReservedAfter { get; set; }

        /// <summary>Set for Reserve, Release and Deduct: which reservation line caused the change.</summary>
        public int? ReservationLineId { get; set; }
        public ReservationLine? ReservationLine { get; set; }

        public AdjustmentReason? ReasonCode { get; set; }
        public string? Reason { get; set; }

        public ActorType ActorType { get; set; }
        public int? PerformedByUserId { get; set; }

        /// <summary>
        /// Sent by the client to make a request safe to repeat: the same id twice never
        /// changes stock twice (double click, network retry).
        /// </summary>
        public Guid? ClientRequestId { get; set; }

        public Guid? CorrelationId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
