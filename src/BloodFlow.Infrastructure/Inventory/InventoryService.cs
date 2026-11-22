using BloodFlow.Application.Common;
using BloodFlow.Application.Inventory;
using BloodFlow.Domain.Entities;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Common;
using BloodFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Inventory
{
    internal class InventoryService : IInventoryService
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _currentUser;
        private readonly IClock _clock;
        private readonly AuditLogger _audit;
        private readonly SettingsReader _settings;
        private readonly InventorySummaryBuilder _summaryBuilder;

        public InventoryService(
            AppDbContext db,
            ICurrentUser currentUser,
            IClock clock,
            AuditLogger audit,
            SettingsReader settings,
            InventorySummaryBuilder summaryBuilder)
        {
            _db = db;
            _currentUser = currentUser;
            _clock = clock;
            _audit = audit;
            _settings = settings;
            _summaryBuilder = summaryBuilder;
        }

        // ================= Intake =================

        public async Task<Result<BatchDto>> IntakeAsync(IntakeBatchRequest request, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var organizationId, out var userId, out var callerError)) return callerError!;

            // Repeated request (double click / retry): return the batch it already created.
            if (request.ClientRequestId.HasValue)
            {
                var replay = await FindReplayAsync(request.ClientRequestId.Value, organizationId, InventoryTransactionType.Intake, cancellationToken);
                if (replay.Error is not null) return replay.Error;
                if (replay.BatchId.HasValue) return await GetBatchAsync(replay.BatchId.Value, cancellationToken);
            }

            if (!await _db.BloodGroups.AnyAsync(g => g.Id == request.BloodGroupId, cancellationToken))
            {
                return Error.Validation("Inventory.BloodGroupNotFound", $"Blood group {request.BloodGroupId} does not exist.");
            }

            var today = _clock.Today;
            var shelfLife = await _settings.GetIntAsync(SettingKeys.DefaultShelfLifeDays, 35, cancellationToken);

            var ruleError = InventoryRules.ValidateIntake(
                request.Quantity, request.CollectionDate!.Value, request.ExpiryDate, today, shelfLife,
                out var expiryDate, out var expiryBasis);
            if (ruleError is not null) return ruleError;

            var now = _clock.UtcNow;
            var batch = new BloodBatch
            {
                OrganizationId = organizationId,
                BloodGroupId = request.BloodGroupId,
                SourceType = BatchSourceType.ManualIntake,
                OriginalCollectionDate = request.CollectionDate.Value,
                ExpiryDate = expiryDate,
                ExpiryBasis = expiryBasis,
                InitialQuantity = request.Quantity,
                OnHandQuantity = request.Quantity,
                ReservedQuantity = 0,
                Status = BatchStatus.Active,
                CreatedAt = now,
                CreatedByUserId = userId
            };

            // The ledger row is added through the navigation, so EF inserts the batch first
            // and fills BatchId automatically, all in the same SaveChanges.
            batch.Transactions.Add(new InventoryTransaction
            {
                OrganizationId = organizationId,
                TransactionType = InventoryTransactionType.Intake,
                OnHandDelta = request.Quantity,
                ReservedDelta = 0,
                OnHandAfter = request.Quantity,
                ReservedAfter = 0,
                ActorType = ActorType.User,
                PerformedByUserId = userId,
                ClientRequestId = request.ClientRequestId,
                CreatedAt = now
            });

            // The audit row needs the new batch Id, so: save, audit, save — inside one transaction.
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            _db.BloodBatches.Add(batch);
            await _db.SaveChangesAsync(cancellationToken);

            _audit.Add(AuditActions.BatchIntake, AuditEntityTypes.BloodBatch, batch.Id.ToString(),
                newValues: new
                {
                    batch.BloodGroupId,
                    Quantity = batch.InitialQuantity,
                    CollectionDate = batch.OriginalCollectionDate,
                    batch.ExpiryDate,
                    batch.ExpiryBasis
                },
                organizationId: organizationId);
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await GetBatchAsync(batch.Id, cancellationToken);
        }

        // ================= Adjust =================

        public async Task<Result<BatchDto>> AdjustAsync(int batchId, AdjustBatchRequest request, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var organizationId, out var userId, out var callerError)) return callerError!;

            if (request.ClientRequestId.HasValue)
            {
                var replay = await FindReplayAsync(request.ClientRequestId.Value, organizationId, InventoryTransactionType.Adjust, cancellationToken);
                if (replay.Error is not null) return replay.Error;
                if (replay.BatchId.HasValue)
                {
                    return replay.BatchId.Value == batchId
                        ? await GetBatchAsync(batchId, cancellationToken)
                        : DuplicateRequestId();
                }
            }

            // Another organization's batch looks exactly like a missing one (no information leak).
            var batch = await _db.BloodBatches
                .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganizationId == organizationId, cancellationToken);
            if (batch is null) return BatchNotFound(batchId);

            var today = _clock.Today;
            var now = _clock.UtcNow;

            // The daily job may not have run yet: a batch past its date is treated as expired now.
            var expiredNow = batch.Status == BatchStatus.Active && InventoryRules.IsExpired(batch.ExpiryDate, today);
            if (expiredNow)
            {
                batch.Status = BatchStatus.Expired;
                batch.ExpiredAt = now;
            }

            var reason = request.ReasonCode!.Value;
            var ruleError = InventoryRules.ValidateAdjustment(
                batch.Status, batch.OnHandQuantity, batch.ReservedQuantity, request.QuantityDelta, reason);
            if (ruleError is not null) return ruleError;

            var oldOnHand = batch.OnHandQuantity;
            batch.OnHandQuantity += request.QuantityDelta;

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                BatchId = batch.Id,
                OrganizationId = organizationId,
                TransactionType = InventoryTransactionType.Adjust,
                OnHandDelta = request.QuantityDelta,
                ReservedDelta = 0,
                OnHandAfter = batch.OnHandQuantity,
                ReservedAfter = batch.ReservedQuantity,
                ReasonCode = reason,
                Reason = request.Reason.Trim(),
                ActorType = ActorType.User,
                PerformedByUserId = userId,
                ClientRequestId = request.ClientRequestId,
                CreatedAt = now
            });

            _audit.Add(AuditActions.BatchAdjusted, AuditEntityTypes.BloodBatch, batch.Id.ToString(),
                oldValues: new { OnHandQuantity = oldOnHand },
                newValues: new { batch.OnHandQuantity, ReasonCode = reason, batch.Status },
                reason: request.Reason.Trim(),
                organizationId: organizationId);

            // Batch + ledger row + audit row: one SaveChanges, one database transaction.
            // RowVersion makes a parallel change to the same batch fail instead of overwriting it.
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.Conflict("Inventory.Concurrency", "This batch was changed at the same time. Reload and try again.");
            }

            return await GetBatchAsync(batch.Id, cancellationToken);
        }

        // ================= Reads =================

        public async Task<Result<PagedResult<BatchDto>>> GetBatchesAsync(BatchQuery query, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var organizationId, out _, out var callerError)) return callerError!;

            var today = _clock.Today;
            var batches = _db.BloodBatches.AsNoTracking().Where(b => b.OrganizationId == organizationId);

            if (query.HideEmpty)
                batches = batches.Where(b => b.OnHandQuantity > 0);
            if (query.BloodGroupId.HasValue)
                batches = batches.Where(b => b.BloodGroupId == query.BloodGroupId.Value);

            // "Expired" also covers Active batches whose date passed before the daily job ran.
            if (query.Status == BatchStatus.Active)
                batches = batches.Where(b => b.Status == BatchStatus.Active && b.ExpiryDate >= today);
            else if (query.Status == BatchStatus.Expired)
                batches = batches.Where(b => b.Status == BatchStatus.Expired || b.ExpiryDate < today);

            if (query.ExpiringWithinDays.HasValue)
            {
                var limit = today.AddDays(Math.Max(0, query.ExpiringWithinDays.Value));
                batches = batches.Where(b => b.Status == BatchStatus.Active && b.ExpiryDate >= today && b.ExpiryDate <= limit);
            }

            var total = await batches.CountAsync(cancellationToken);

            // FEFO order: the batch that expires first is shown (and later used) first.
            var rows = await batches
                .OrderBy(b => b.ExpiryDate).ThenBy(b => b.Id)
                .Skip(query.Skip)
                .Take(query.SafePageSize)
                .Select(BatchRow.Projection)
                .ToListAsync(cancellationToken);

            return new PagedResult<BatchDto>(rows.Select(r => r.ToDto(today)).ToList(), query.SafePage, query.SafePageSize, total);
        }

        public async Task<Result<BatchDto>> GetBatchAsync(int batchId, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var organizationId, out _, out var callerError)) return callerError!;

            var row = await _db.BloodBatches.AsNoTracking()
                .Where(b => b.Id == batchId && b.OrganizationId == organizationId)
                .Select(BatchRow.Projection)
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null) return BatchNotFound(batchId);
            return row.ToDto(_clock.Today);
        }

        public async Task<Result<PagedResult<InventoryTransactionDto>>> GetLedgerAsync(LedgerQuery query, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var organizationId, out _, out var callerError)) return callerError!;

            var ledger = _db.InventoryTransactions.AsNoTracking().Where(t => t.OrganizationId == organizationId);

            if (query.BatchId.HasValue)
                ledger = ledger.Where(t => t.BatchId == query.BatchId.Value);
            if (query.Type.HasValue)
                ledger = ledger.Where(t => t.TransactionType == query.Type.Value);
            if (query.FromUtc.HasValue)
                ledger = ledger.Where(t => t.CreatedAt >= query.FromUtc.Value);
            if (query.ToUtc.HasValue)
                ledger = ledger.Where(t => t.CreatedAt < query.ToUtc.Value);

            var total = await ledger.CountAsync(cancellationToken);

            var items = await ledger
                .OrderByDescending(t => t.Id)
                .Skip(query.Skip)
                .Take(query.SafePageSize)
                .Select(t => new InventoryTransactionDto(
                    t.Id, t.BatchId, t.Batch.BloodGroup.Code, t.TransactionType,
                    t.OnHandDelta, t.ReservedDelta, t.OnHandAfter, t.ReservedAfter,
                    t.ReasonCode, t.Reason, t.ActorType, t.PerformedByUserId, t.CreatedAt))
                .ToListAsync(cancellationToken);

            return new PagedResult<InventoryTransactionDto>(items, query.SafePage, query.SafePageSize, total);
        }

        public async Task<Result<InventorySummaryDto>> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var organizationId, out _, out var callerError)) return callerError!;
            return await _summaryBuilder.BuildAsync(organizationId, cancellationToken);
        }

        // ================= helpers =================

        /// <summary>Staff always act on their own organization, taken from the token.</summary>
        private bool TryGetCaller(out int organizationId, out int userId, out Error? error)
        {
            organizationId = _currentUser.OrganizationId ?? 0;
            userId = _currentUser.UserId ?? 0;
            error = null;

            if (_currentUser.OrganizationId is null || _currentUser.UserId is null)
            {
                error = Error.Forbidden("Inventory.NoOrganization", "Only hospital or blood bank staff can use inventory.");
                return false;
            }
            return true;
        }

        private async Task<(int? BatchId, Error? Error)> FindReplayAsync(
            Guid clientRequestId, int organizationId, InventoryTransactionType type, CancellationToken cancellationToken)
        {
            var previous = await _db.InventoryTransactions.AsNoTracking()
                .Where(t => t.ClientRequestId == clientRequestId)
                .Select(t => new { t.BatchId, t.OrganizationId, t.TransactionType })
                .FirstOrDefaultAsync(cancellationToken);

            if (previous is null) return (null, null);

            return previous.OrganizationId == organizationId && previous.TransactionType == type
                ? (previous.BatchId, null)
                : (null, DuplicateRequestId());
        }

        private static Error BatchNotFound(int id) =>
            Error.NotFound("Inventory.BatchNotFound", $"Batch {id} was not found.");

        private static Error DuplicateRequestId() =>
            Error.Conflict("Inventory.DuplicateRequestId", "This clientRequestId was already used for a different operation.");
    }
}
