using BloodFlow.Domain.Entities;
using BloodFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodFlow.Infrastructure.Persistence.Configurations
{
    internal class BloodRequestConfiguration : IEntityTypeConfiguration<BloodRequest>
    {
        public const string NumberSequence = "BloodRequestNumbers";

        public void Configure(EntityTypeBuilder<BloodRequest> builder)
        {
            builder.HasKey(r => r.Id);

            builder.Property(r => r.RequestNumber).HasMaxLength(20).IsUnicode(false);
            builder.HasIndex(r => r.RequestNumber).IsUnique();

            builder.Property(r => r.RequestType).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
            builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
            builder.Property(r => r.VerificationStatus).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
            builder.Property(r => r.DestinationOrgType).HasConversion<string>().HasMaxLength(12).IsUnicode(false);
            builder.Property(r => r.RequesterCapacity).HasConversion<string>().HasMaxLength(15).IsUnicode(false);

            builder.Property(r => r.PatientFullName).HasMaxLength(200);
            builder.Property(r => r.RelationshipToPatient).HasMaxLength(100);

            builder.Property(r => r.RowVersion).IsRowVersion();

            builder.HasOne(r => r.BloodGroup)
                .WithMany()
                .HasForeignKey(r => r.BloodGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            // Composite FK (DestinationHospitalId, DestinationOrgType) → Organizations(Id, Type):
            // with DestinationOrgType fixed to 'Hospital', the destination can only ever be a hospital.
            builder.HasOne(r => r.DestinationHospital)
                .WithMany()
                .HasForeignKey(r => new { r.DestinationHospitalId, r.DestinationOrgType })
                .HasPrincipalKey(o => new { o.Id, o.Type })
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.VerifiedByUserId).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(r => r.ClientRequestId).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");

            // Boards and monitoring: open requests by status, emergencies first, by deadline.
            builder.HasIndex(r => new { r.Status, r.RequestType, r.NeededBy });
            builder.HasIndex(r => new { r.DestinationHospitalId, r.Status });
            // Duplicate protection lookup (same requester, patient group and hospital within 24 h).
            builder.HasIndex(r => new { r.CreatedByUserId, r.BloodGroupId, r.DestinationHospitalId, r.CreatedAt });

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_BloodRequests_Units", "[UnitsRequested] > 0");
                t.HasCheckConstraint("CK_BloodRequests_DestinationIsHospital", "[DestinationOrgType] = 'Hospital'");
                t.HasCheckConstraint("CK_BloodRequests_Type", "[RequestType] IN ('Normal', 'Emergency')");
                t.HasCheckConstraint("CK_BloodRequests_Status",
                    "[Status] IN ('Pending', 'Approved', 'Fulfilled', 'Received', 'Rejected', 'Cancelled')");
                t.HasCheckConstraint("CK_BloodRequests_Verification", "[VerificationStatus] IN ('Unverified', 'Verified')");
                t.HasCheckConstraint("CK_BloodRequests_Capacity",
                    "[RequesterCapacity] IN ('HospitalStaff', 'Patient', 'Representative')");
                // Blood only leaves for a verified request, unless an audited emergency override was used.
                t.HasCheckConstraint("CK_BloodRequests_FulfilledIsVerified",
                    "[Status] NOT IN ('Fulfilled', 'Received') OR [VerificationStatus] = 'Verified' OR [EmergencyOverrideUsed] = 1");
                // After anonymization the name must be gone.
                t.HasCheckConstraint("CK_BloodRequests_Anonymized",
                    "[PatientAnonymizedAt] IS NULL OR [PatientFullName] IS NULL");
            });
        }
    }

    internal class BloodRequestStatusHistoryConfiguration : IEntityTypeConfiguration<BloodRequestStatusHistory>
    {
        public void Configure(EntityTypeBuilder<BloodRequestStatusHistory> builder)
        {
            builder.ToTable("BloodRequestStatusHistory");
            builder.HasKey(h => h.Id);

            builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
            builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
            builder.Property(h => h.Reason).HasMaxLength(500);

            builder.HasOne(h => h.Request)
                .WithMany(r => r.StatusHistory)
                .HasForeignKey(h => h.RequestId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);

            // Each status is reached at most once per request.
            builder.HasIndex(h => new { h.RequestId, h.ToStatus }).IsUnique();

            builder.ToTable(t => t.HasCheckConstraint("CK_BloodRequestStatusHistory_Transition",
                "([FromStatus] IS NULL AND [ToStatus] = 'Pending')"
                + " OR ([FromStatus] = 'Pending' AND [ToStatus] IN ('Approved', 'Rejected', 'Cancelled'))"
                + " OR ([FromStatus] = 'Approved' AND [ToStatus] IN ('Fulfilled', 'Cancelled'))"
                + " OR ([FromStatus] = 'Fulfilled' AND [ToStatus] = 'Received')"));
        }
    }
}
