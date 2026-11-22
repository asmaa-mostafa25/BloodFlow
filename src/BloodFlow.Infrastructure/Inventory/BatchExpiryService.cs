using BloodFlow.Application.Common;
using BloodFlow.Application.Inventory;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Common;
using BloodFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

                // Slice 3: if the batch has active reservation lines, mark them Invalidated
                // here and alert the staff (workflow B1).
            }

            if (due.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            return due.Count;
        }
    }

    /// <summary>
    /// Runs the expiry check when the API starts and then every hour.
    /// Hourly (not once a day) so a restart or a missed run is caught quickly;
    /// the check is idempotent, so running it often is harmless.
    /// </summary>
    internal sealed class BatchExpiryBackgroundService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BatchExpiryBackgroundService> _logger;

        public BatchExpiryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<BatchExpiryBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    // A background job has no HTTP request, so it opens its own scope (the "table" 🍽️).
                    using var scope = _scopeFactory.CreateScope();
                    var expiry = scope.ServiceProvider.GetRequiredService<IBatchExpiryService>();
                    var count = await expiry.ExpireDueBatchesAsync(stoppingToken);
                    if (count > 0)
                    {
                        _logger.LogInformation("Marked {Count} blood batches as expired.", count);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Never crash the API because of the job; try again next hour.
                    _logger.LogError(ex, "Batch expiry job failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
