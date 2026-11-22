using BloodFlow.Application.Common;
using BloodFlow.Application.Inventory;
using BloodFlow.Domain.Entities;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Common;
using BloodFlow.Infrastructure.Persistence;
using BloodFlow.Infrastructure.Requests;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Inventory
{
    /// <summary>Workflow B1 "daily task": mark batches past their expiry date as Expired.</summary>
    internal sealed class BatchExpiryService : IBatchExpiryService
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly AuditLogger _audit;

        public BatchExpiryService(AppDbContext db, IClock clock, AuditLogger audit)
        {
            _db = db;
            _clock = clock;
            _audit = audit;
        }

        public async Task<int> ExpireDueBatchesAsync(CancellationToken cancellationToken = default)
        {
            var today = _clock.Today;
            var now = _clock.UtcNow;

            var due = await _db.BloodBatches
                .Where(b => b.Status == BatchStatus.Active && b.ExpiryDate < today)
                .ToListAsync(cancellationToken);

            foreach (var batch in due)
            {
                batch.Status = BatchStatus.Expired;
                batch.ExpiredAt = now;

                // No user is signed in here, so the audit row is a "System" action.
                _audit.Add(AuditActions.BatchExpired, AuditEntityTypes.BloodBatch, batch.Id.ToString(),
                    oldValues: new { Status = BatchStatus.Active },
                    newValues: new { batch.Status, batch.ExpiryDate, batch.OnHandQuantity, batch.ReservedQuantity },
                    organizationId: batch.OrganizationId);
            }

            // Slice 3: units reserved on an expired batch can never be sent. Release them with
            // status Invalidated; the request then shows "needs reallocation" to the blood bank.
            var dueIds = due.Select(b => b.Id).ToList();
            var lines = dueIds.Count == 0
                ? new List<ReservationLine>()
                : await _db.ReservationLines
                    .Include(l => l.Reservation)
                    .Where(l => dueIds.Contains(l.BatchId) && l.Status == ReservationLineStatus.Active)
                    .ToListAsync(cancellationToken);

            var correlationId = Guid.NewGuid();
            var batchesById = due.ToDictionary(b => b.Id);
            foreach (var line in lines)
            {
                // userId null → the ledger row is a System action.
                _db.InventoryTransactions.Add(ReservationLedger.Release(
                    batchesById[line.BatchId], line, ReservationLineStatus.Invalidated, "BatchExpired",
                    userId: null, correlationId, now));

                _audit.Add(AuditActions.ReservationLineInvalidated, AuditEntityTypes.BloodRequest, line.Reservation.RequestId.ToString(),
                    newValues: new { line.BatchId, line.Quantity, Reason = "BatchExpired" },
                    organizationId: line.OrganizationId);
            }

            if (due.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            return due.Count;
        }
    }
}
