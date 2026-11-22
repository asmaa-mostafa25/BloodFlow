namespace BloodFlow.Application.Common
{
    /// <summary>Keys of the rows in SystemSettings.</summary>
    public static class SettingKeys
    {
        public const string MinimumDonorAgeYears = "Donation.MinimumDonorAgeYears";
        public const string DonationIntervalDays = "Donation.IntervalDays";
        public const string DefaultShelfLifeDays = "Inventory.DefaultShelfLifeDays";
        public const string MaxUnitsPerRequest = "Requests.MaxUnitsPerRequest";
        public const string NearExpiryAlertDays = "Inventory.NearExpiryAlertDays";
        public const string EmergencyEscalationMinutes = "Requests.EmergencyEscalationMinutes";
        public const string ReceiptConfirmationHours = "Requests.ReceiptConfirmationHours";
        public const string DuplicateRequestWindowHours = "Requests.DuplicateWindowHours";
        public const string PatientNameRetentionMonths = "Privacy.PatientNameRetentionMonths";
    }
}
