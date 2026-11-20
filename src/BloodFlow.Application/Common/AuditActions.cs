namespace BloodFlow.Application.Common
{
    /// <summary>Names written to AuditLogs.Action. One place, so they never drift.</summary>
    public static class AuditActions
    {
        public const string StaffRegistered = "Staff.Registered";
        public const string StaffApproved = "Staff.Approved";
        public const string StaffRejected = "Staff.Rejected";
        public const string StaffDeactivated = "Staff.Deactivated";
        public const string StaffReactivated = "Staff.Reactivated";

        public const string OrganizationCreated = "Organization.Created";
        public const string OrganizationUpdated = "Organization.Updated";
        public const string OrganizationDeactivated = "Organization.Deactivated";
        public const string OrganizationActivated = "Organization.Activated";

        public const string SettingUpdated = "Setting.Updated";

        public const string BatchIntake = "Batch.Intake";
        public const string BatchAdjusted = "Batch.Adjusted";
        public const string BatchExpired = "Batch.Expired";
    }

    public static class AuditEntityTypes
    {
        public const string User = "User";
        public const string Organization = "Organization";
        public const string SystemSetting = "SystemSetting";
        public const string BloodBatch = "BloodBatch";
    }
}
