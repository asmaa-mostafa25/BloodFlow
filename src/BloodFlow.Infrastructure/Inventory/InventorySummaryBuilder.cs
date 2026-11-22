using BloodFlow.Application.Common;
using BloodFlow.Application.Inventory;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Common;
using BloodFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Inventory
{
    /// <summary>
    /// Builds the per-blood-group stock picture for one organization.
    /// Shared by the staff screen and the admin's read-only report.
    /// </summary>
    internal sealed class InventorySummaryBuilder
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly SettingsReader _settings;

        public InventorySummaryBuilder(AppDbContext db, IClock clock, SettingsReader settings)
        {
            _db = db;
            _clock = clock;
            _settings = settings;
        }

        public async Task<Result<InventorySummaryDto>> BuildAsync(int organizationId, CancellationToken cancellationToken)
        {
            var organizationName = await _db.Organizations.AsNoTracking()
                .Where(o => o.Id == organizationId)
                .Select(o => o.Name)
                .FirstOrDefaultAsync(cancellationToken);
            if (organizationName is null)
            {
                return Error.NotFound("Organization.NotFound", $"Organization {organizationId} was not found.");
            }

            var today = _clock.Today;
            var nearExpiryDays = await _settings.GetIntAsync(SettingKeys.NearExpiryAlertDays, 7, cancellationToken);
            var nearLimit = today.AddDays(nearExpiryDays);

            var bloodGroups = await _db.BloodGroups.AsNoTracking()
                .OrderBy(g => g.Id)
                .Select(g => new { g.Id, g.Code })
                .ToListAsync(cancellationToken);

            // Only batches with stock left. An organization holds tens or hundreds of these,
            // so grouping in memory is simple and fast enough for the MVP.
            var batches = await _db.BloodBatches.AsNoTracking()
                .Where(b => b.OrganizationId == organizationId && b.OnHandQuantity > 0)
                .Select(b => new { b.BloodGroupId, b.OnHandQuantity, b.ReservedQuantity, b.ExpiryDate, b.Status })
                .ToListAsync(cancellationToken);

            var levels = bloodGroups.Select(g =>
            {
                var ofGroup = batches.Where(b => b.BloodGroupId == g.Id).ToList();
                bool IsUsable(BatchStatus status, DateOnly expiry) =>
                    status == BatchStatus.Active && !InventoryRules.IsExpired(expiry, today);

                var usable = ofGroup.Where(b => IsUsable(b.Status, b.ExpiryDate)).ToList();
                var expired = ofGroup.Where(b => !IsUsable(b.Status, b.ExpiryDate)).ToList();

                return new StockLevelDto(
                    g.Id,
                    g.Code,
                    OnHand: ofGroup.Sum(b => b.OnHandQuantity),
                    Reserved: ofGroup.Sum(b => b.ReservedQuantity),
                    Available: usable.Sum(b => b.OnHandQuantity - b.ReservedQuantity),
                    NearExpiryUnits: usable.Where(b => b.ExpiryDate <= nearLimit).Sum(b => b.OnHandQuantity - b.ReservedQuantity),
                    ExpiredUnits: expired.Sum(b => b.OnHandQuantity),
                    NextExpiryDate: usable.Where(b => b.OnHandQuantity > b.ReservedQuantity)
                        .Select(b => (DateOnly?)b.ExpiryDate).Min());
            }).ToList();

            return new InventorySummaryDto(organizationId, organizationName, today, nearExpiryDays, levels);
        }
    }

    internal sealed class InventoryReportService : IInventoryReportService
    {
        private readonly InventorySummaryBuilder _builder;

        public InventoryReportService(InventorySummaryBuilder builder) => _builder = builder;

        public Task<Result<InventorySummaryDto>> GetSummaryAsync(int organizationId, CancellationToken cancellationToken = default) =>
            _builder.BuildAsync(organizationId, cancellationToken);
    }
}
