using BloodFlow.Application.Inventory;
using BloodFlow.Application.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BloodFlow.Infrastructure.Jobs
{
    /// <summary>
    /// One hourly job for all periodic work (replaces the Slice 2 expiry-only job):
    ///   1. expire batches past their date and invalidate their reservation lines;
    ///   2. anonymize old patient names.
    /// Hourly (not once a day) so a restart or a missed run is caught quickly; every step
    /// is idempotent, so running it often is harmless. Each step has its own scope and its
    /// own try/catch: one failing step never blocks the other.
    /// </summary>
    internal sealed class MaintenanceBackgroundService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MaintenanceBackgroundService> _logger;

        public MaintenanceBackgroundService(IServiceScopeFactory scopeFactory, ILogger<MaintenanceBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await RunStepAsync("Batch expiry", async sp =>
                {
                    var count = await sp.GetRequiredService<IBatchExpiryService>().ExpireDueBatchesAsync(stoppingToken);
                    if (count > 0) _logger.LogInformation("Marked {Count} blood batches as expired.", count);
                }, stoppingToken);

                await RunStepAsync("Patient name anonymization", async sp =>
                {
                    var count = await sp.GetRequiredService<IRequestMaintenanceService>().AnonymizePatientNamesAsync(stoppingToken);
                    if (count > 0) _logger.LogInformation("Anonymized patient names on {Count} requests.", count);
                }, stoppingToken);
            }
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task RunStepAsync(string name, Func<IServiceProvider, Task> step, CancellationToken stoppingToken)
        {
            try
            {
                // A background job has no HTTP request, so it opens its own DI scope (its own DbContext).
                using var scope = _scopeFactory.CreateScope();
                await step(scope.ServiceProvider);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutting down
            }
            catch (Exception ex)
            {
                // Never crash the API because of a job; try again next hour.
                _logger.LogError(ex, "{Step} job failed.", name);
            }
        }
    }
}
