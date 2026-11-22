using BloodFlow.Domain.Enums;

namespace BloodFlow.Domain.Entities
{
    /// <summary>
    /// Extra data for Hospital Staff and Blood Bank Staff accounts.
    /// One row per staff user (the primary key is also the foreign key to the user).
    /// The account status itself lives on the user (AspNetUsers.AccountStatus).
    /// </summary>
    public class StaffProfile
    {
        /// <summary>Primary key and FK to AspNetUsers.Id (one-to-one).</summary>
        public int UserId { get; set; }

        public int OrganizationId { get; set; }
        public Organization Organization { get; set; } = null!;

        /// <summary>
        /// Must equal the organization's Type. Enforced in the database by a
        /// composite FK (OrganizationId, StaffKind) → Organizations(Id, Type).
        /// </summary>
        public OrganizationType StaffKind { get; set; }

        public required string JobTitle { get; set; }

        public int? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public string? RejectionReason { get; set; }
    }
}
