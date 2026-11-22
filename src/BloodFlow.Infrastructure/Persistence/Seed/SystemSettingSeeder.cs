using BloodFlow.Application.Common;
using BloodFlow.Domain.Entities;
using BloodFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Persistence.Seed
{
    /// <summary>
    /// Inserts the default system settings that are missing. Existing rows are never touched,
    /// so a value the admin changed is never overwritten by a code default.
    /// (With HasData, changing a default in code would generate an UPDATE that wipes the admin's value.)
    /// </summary>
    internal static class SystemSettingSeeder
    {
        // Defaults from the Phase 1 workflows. Medical values are still assumptions waiting
        // for the supervisor's confirmation, so they have no ApprovalReference yet.
        private static readonly SystemSetting[] Defaults =
        {
            Int(SettingKeys.MinimumDonorAgeYears, "18", "Minimum donor age in years.", medical: true),
            Int(SettingKeys.DonationIntervalDays, "56", "Minimum days between two verified donations.", medical: true),
            Int(SettingKeys.DefaultShelfLifeDays, "35", "Default blood unit shelf life in days (collection date + N).", medical: true),
            Int(SettingKeys.MaxUnitsPerRequest, "10", "Maximum units allowed in one blood request.", medical: true),
            Int(SettingKeys.NearExpiryAlertDays, "7", "Alert staff when a batch expires within N days."),
            Int(SettingKeys.EmergencyEscalationMinutes, "30", "Escalate an emergency request not actioned within N minutes."),
            Int(SettingKeys.ReceiptConfirmationHours, "48", "Mark a fulfilled request Unconfirmed after N hours without receipt."),
            Int(SettingKeys.DuplicateRequestWindowHours, "24", "Reject a duplicate open request within N hours."),
            Int(SettingKeys.PatientNameRetentionMonths, "12", "Anonymize patient names after N months.")
        };

        public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
        {
            var existingKeys = await db.SystemSettings
                .Select(s => s.SettingKey)
                .ToListAsync(cancellationToken);

            var missing = Defaults.Where(d => !existingKeys.Contains(d.SettingKey)).ToList();
            if (missing.Count == 0)
            {
                return;
            }

            // New objects each time: the static array is a template, never tracked by EF.
            db.SystemSettings.AddRange(missing.Select(d => new SystemSetting
            {
                SettingKey = d.SettingKey,
                SettingValue = d.SettingValue,
                ValueType = d.ValueType,
                Description = d.Description,
                IsMedicalParameter = d.IsMedicalParameter
            }));

            await db.SaveChangesAsync(cancellationToken);
        }

        private static SystemSetting Int(string key, string value, string description, bool medical = false) => new()
        {
            SettingKey = key,
            SettingValue = value,
            ValueType = SettingValueType.Int,
            Description = description,
            IsMedicalParameter = medical
        };
    }
}
