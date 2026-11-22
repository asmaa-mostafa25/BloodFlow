namespace BloodFlow.Application.Common
{
    public static class Roles
    {
        public const string SystemAdmin = "SystemAdmin";
        public const string HospitalStaff = "HospitalStaff";
        public const string BloodBankStaff = "BloodBankStaff";
        public const string Donor = "Donor";
        public const string Recipient = "Recipient";

        /// <summary>
        /// For [Authorize(Roles = Roles.Staff)]: a comma means "any of these roles".
        /// Hospital and blood bank staff share the inventory rules (workflows B1 and H4).
        /// </summary>
        public const string Staff = HospitalStaff + "," + BloodBankStaff;

        public static readonly string[] AllRoles =
        {
            SystemAdmin,
            HospitalStaff,
            BloodBankStaff,
            Donor,
            Recipient
        };
    }
}
