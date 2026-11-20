using System.ComponentModel.DataAnnotations;
using BloodFlow.Application.Common;
using BloodFlow.Domain.Enums;

namespace BloodFlow.Application.Inventory
{
    public sealed record IntakeBatchRequest
    {
        [Range(1, 8)]
        public byte BloodGroupId { get; init; }

        [Range(1, 1000)]
        public int Quantity { get; init; }

        [Required]
        public DateOnly? CollectionDate { get; init; }

        /// <summary>Optional. When empty: collection date + the default shelf life setting.</summary>
        public DateOnly? ExpiryDate { get; init; }

        /// <summary>Optional but recommended: a new GUID per form submission makes retries safe.</summary>
        public Guid? ClientRequestId { get; init; }
    }

    public sealed record AdjustBatchRequest
    {
        /// <summary>Negative to remove units, positive to add (Correction only).</summary>
        [Range(-1000, 1000)]
        public int QuantityDelta { get; init; }

        [Required, EnumDataType(typeof(AdjustmentReason))]
        public AdjustmentReason? ReasonCode { get; init; }

        [Required, StringLength(500, MinimumLength = 3)]
        public string Reason { get; init; } = string.Empty;

        public Guid? ClientRequestId { get; init; }
    }

    public sealed record BatchQuery : PageQuery
    {
        public byte? BloodGroupId { get; init; }
        public BatchStatus? Status { get; init; }

        /// <summary>Only Active batches expiring within N days.</summary>
        public int? ExpiringWithinDays { get; init; }

        /// <summary>Hide batches with nothing left on hand (default true).</summary>
        public bool HideEmpty { get; init; } = true;
    }

    public sealed record LedgerQuery : PageQuery
    {
        public int? BatchId { get; init; }
        public InventoryTransactionType? Type { get; init; }
        public DateTime? FromUtc { get; init; }
        public DateTime? ToUtc { get; init; }
    }

    public sealed record BatchDto(
        int Id,
        byte BloodGroupId,
        string BloodGroupCode,
        BatchSourceType SourceType,
        DateOnly CollectionDate,
        DateOnly ExpiryDate,
        ExpiryBasis ExpiryBasis,
        int DaysToExpiry,
        int InitialQuantity,
        int OnHandQuantity,
        int ReservedQuantity,
        int AvailableQuantity,
        BatchStatus Status,
        DateTime CreatedAt);

    public sealed record InventoryTransactionDto(
        long Id,
        int BatchId,
        string BloodGroupCode,
        InventoryTransactionType Type,
        int OnHandDelta,
        int ReservedDelta,
        int OnHandAfter,
        int ReservedAfter,
        AdjustmentReason? ReasonCode,
        string? Reason,
        ActorType ActorType,
        int? PerformedByUserId,
        DateTime CreatedAt);

    /// <summary>One row per blood group: the numbers a stock screen shows.</summary>
    public sealed record StockLevelDto(
        byte BloodGroupId,
        string BloodGroupCode,
        int OnHand,
        int Reserved,
        int Available,
        int NearExpiryUnits,
        int ExpiredUnits,
        DateOnly? NextExpiryDate);

    public sealed record InventorySummaryDto(
        int OrganizationId,
        string OrganizationName,
        DateOnly Today,
        int NearExpiryDays,
        IReadOnlyList<StockLevelDto> BloodGroups);
}
