using BloodFlow.Application.Common;
using BloodFlow.Application.Requests;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Common;
using BloodFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Requests
{
    /// <summary>
    /// Patient privacy (Phase 1): once a request is finished (Received, Rejected or Cancelled)
    /// and older than Privacy.PatientNameRetentionMonths, the patient name is removed for good.
    /// The request itself (numbers, dates, units) stays for statistics and audit.
    /// </summary>
    internal sealed class RequestMaintenanceService : IRequestMaintenanceService
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly AuditLogger _audit;
        private readonly SettingsReader _settings;

        public RequestMaintenanceService(AppDbContext db, IClock clock, AuditLogger audit, SettingsReader settings)
        {
            _db = db;
            _clock = clock;
            _audit = audit;
            _settings = settings;
        }

        public async Task<int> AnonymizePatientNamesAsync(CancellationToken cancellationToken = default)
        {
            var months = await _settings.GetIntAsync(SettingKeys.PatientNameRetentionMonths, 12, cancellationToken);
            var now = _clock.UtcNow;
            var cutoff = now.AddMonths(-months);

            var due = _db.BloodRequests.Where(r =>
                r.PatientAnonymizedAt == null
                && r.CreatedAt < cutoff
                && (r.Status == RequestStatus.Received || r.Status == RequestStatus.Rejected || r.Status == RequestStatus.Cancelled));

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            // One UPDATE statement for all rows (no loading into memory).
            var count = await due.ExecuteUpdateAsync(s => s
                .SetProperty(r => r.PatientFullName, (string?)null)
                .SetProperty(r => r.RelationshipToPatient, (string?)null)
                .SetProperty(r => r.PatientAnonymizedAt, now), cancellationToken);

            if (count > 0)
            {
                // One summary row (System actor): how many, and the rule that was applied.
                _audit.Add(AuditActions.PatientAnonymized, AuditEntityTypes.BloodRequest, entityId: null,
                    newValues: new { Count = count, RetentionMonths = months, CreatedBefore = cutoff });
                await _db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return count;
        }
    }
}
