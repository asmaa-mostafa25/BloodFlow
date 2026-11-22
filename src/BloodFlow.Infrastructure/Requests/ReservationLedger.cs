using BloodFlow.Domain.Entities;
using BloodFlow.Domain.Enums;

namespace BloodFlow.Infrastructure.Requests
{
    /// <summary>
    /// The ONLY place that moves reserved stock. Every move changes the batch numbers AND
    /// writes the matching ledger row, so the batch and its ledger can never disagree.
    ///   Reserve : Reserved +q                (units held for a request)
    ///   Release : Reserved −q                (hold cancelled, units free again)
    ///   Deduct  : OnHand −q and Reserved −q  (units physically left the bank)
    /// Nothing is saved here: the caller's single SaveChanges writes everything together.
    /// </summary>
    internal static class ReservationLedger
    {
        public static InventoryTransaction Reserve(BloodBatch batch, ReservationLine line, int? userId, Guid correlationId, DateTime now)
        {
            batch.ReservedQuantity += line.Quantity;
            return Row(batch, line, InventoryTransactionType.Reserve, onHandDelta: 0, reservedDelta: line.Quantity, userId, correlationId, now);
        }

        public static InventoryTransaction Release(BloodBatch batch, ReservationLine line, ReservationLineStatus newStatus,
            string? invalidationReason, int? userId, Guid correlationId, DateTime now)
        {
            batch.ReservedQuantity -= line.Quantity;
            line.Status = newStatus;                       // Released or Invalidated
            line.InvalidationReason = invalidationReason;  // only for Invalidated (a CHECK enforces it)
            line.ClosedAt = now;
            return Row(batch, line, InventoryTransactionType.Release, onHandDelta: 0, reservedDelta: -line.Quantity, userId, correlationId, now);
        }

        public static InventoryTransaction Deduct(BloodBatch batch, ReservationLine line, int userId, Guid correlationId, DateTime now)
        {
            batch.OnHandQuantity -= line.Quantity;
            batch.ReservedQuantity -= line.Quantity;
            line.Status = ReservationLineStatus.Consumed;
            line.ClosedAt = now;
            return Row(batch, line, InventoryTransactionType.Deduct, onHandDelta: -line.Quantity, reservedDelta: -line.Quantity, userId, correlationId, now);
        }

        private static InventoryTransaction Row(BloodBatch batch, ReservationLine line, InventoryTransactionType type,
            int onHandDelta, int reservedDelta, int? userId, Guid correlationId, DateTime now) => new()
        {
            BatchId = batch.Id,
            OrganizationId = batch.OrganizationId,
            TransactionType = type,
            OnHandDelta = onHandDelta,
            ReservedDelta = reservedDelta,
            OnHandAfter = batch.OnHandQuantity,
            ReservedAfter = batch.ReservedQuantity,
            // Navigation (not the Id): a brand-new line has no Id yet; EF fills it on save.
            ReservationLine = line,
            // userId null = the background job (System actor).
            ActorType = userId.HasValue ? ActorType.User : ActorType.System,
            PerformedByUserId = userId,
            // All rows of one action (e.g. one approval over 3 batches) share this id.
            CorrelationId = correlationId,
            CreatedAt = now
        };
    }
}
