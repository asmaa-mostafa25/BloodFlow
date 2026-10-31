namespace BloodFlow.Application.Common
{
public static class Roles
    {
        public const string SystemAdmin = "SystemAdmin";
        public const string HospitalStaff = "HospitalStaff";
        public const string BloodBankStaff = "BloodBankStaff";
        public const string Donor = "Donor";
        public const string Recipient = "Recipient";
        public static readonly string[] AllRoles = new string[]
        {
            SystemAdmin,
            HospitalStaff,
            BloodBankStaff,
            Donor,
            Recipient
        };

    }
}
