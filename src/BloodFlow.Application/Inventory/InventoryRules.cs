using BloodFlow.Application.Common;
using BloodFlow.Domain.Enums;

namespace BloodFlow.Application.Inventory
{
    /// <summary>
    /// The stock rules from workflow B1, as pure functions with no database:
    /// easy to read, easy to unit test. The service calls these before changing anything.
    /// </summary>
    public static class InventoryRules
    {
        /// <summary>
        /// Checks a manual intake and decides the expiry date.
        /// Rules: quantity &gt; 0; collected today or earlier; expiry after collection and in the future.
        /// When no expiry date is given, it is collection date + default shelf life.
        /// </summary>
        public static Error? ValidateIntake(
            int quantity,
            DateOnly collectionDate,
            DateOnly? enteredExpiryDate,
            DateOnly today,
            int defaultShelfLifeDays,
            out DateOnly expiryDate,
            out ExpiryBasis expiryBasis)
        {
            expiryBasis = enteredExpiryDate.HasValue ? ExpiryBasis.Entered : ExpiryBasis.DefaultShelfLife;
            expiryDate = enteredExpiryDate ?? collectionDate.AddDays(defaultShelfLifeDays);

            if (quantity <= 0)
                return Error.Validation("Inventory.InvalidQuantity", "Quantity must be greater than zero.");

            if (collectionDate > today)
                return Error.Validation("Inventory.CollectionInFuture", "The collection date cannot be in the future.");

            if (expiryDate <= collectionDate)
                return Error.Validation("Inventory.ExpiryBeforeCollection", "The expiry date must be after the collection date.");

            if (expiryDate <= today)
                return Error.Validation("Inventory.AlreadyExpired",
                    expiryBasis == ExpiryBasis.Entered
                        ? "The expiry date must be in the future."
                        : $"With the default shelf life of {defaultShelfLifeDays} days these units are already expired.");

            return null;
        }

        /// <summary>
        /// Checks a manual adjustment.
        /// Rules: the change is not zero; Damaged/Expired only decrease; an expired batch can
        /// only decrease (disposal); OnHand can never go below Reserved (or below zero).
        /// </summary>
        public static Error? ValidateAdjustment(
            BatchStatus status,
            int onHand,
            int reserved,
            int delta,
            AdjustmentReason reason)
        {
            if (delta == 0)
                return Error.Validation("Inventory.ZeroAdjustment", "The adjustment cannot be zero.");

            if (reason is AdjustmentReason.Damaged or AdjustmentReason.Expired && delta > 0)
                return Error.Validation("Inventory.ReasonRequiresDecrease", $"A '{reason}' adjustment can only decrease the quantity.");

            if (status == BatchStatus.Expired && delta > 0)
                return Error.Validation("Inventory.ExpiredBatchIncrease", "An expired batch can only be decreased (disposed of).");

            var newOnHand = onHand + delta;
            if (newOnHand < 0)
                return Error.Validation("Inventory.NegativeStock", $"Only {onHand} units are on hand.");

            if (newOnHand < reserved)
                return Error.Conflict("Inventory.BelowReserved",
                    $"{reserved} units of this batch are reserved; on-hand cannot go below that.");

            return null;
        }

        /// <summary>A batch is usable until the end of its expiry date.</summary>
        public static bool IsExpired(DateOnly expiryDate, DateOnly today) => expiryDate < today;
    }
}
