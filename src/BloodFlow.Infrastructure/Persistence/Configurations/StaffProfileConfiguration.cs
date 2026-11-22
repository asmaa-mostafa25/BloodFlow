using BloodFlow.Domain.Entities;
using BloodFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodFlow.Infrastructure.Persistence.Configurations
{
    internal class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
    {
        public void Configure(EntityTypeBuilder<StaffProfile> builder)
        {
            builder.HasKey(s => s.UserId);

            // The key comes from the user, never generated here.
            builder.Property(s => s.UserId).ValueGeneratedNever();

            builder.Property(s => s.JobTitle)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(s => s.RejectionReason)
                .HasMaxLength(500);

            builder.Property(s => s.StaffKind)
                .HasConversion<string>()
                .HasMaxLength(12)
                .IsUnicode(false);

            // One-to-one with the user: StaffProfiles.UserId → AspNetUsers.Id
            builder.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<StaffProfile>(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Composite FK (OrganizationId, StaffKind) → Organizations(Id, Type).
            // The database itself guarantees a "Hospital" staff member can only belong
            // to a Hospital, and a "BloodBank" staff member only to a Blood Bank.
            builder.HasOne(s => s.Organization)
                .WithMany()
                .HasForeignKey(s => new { s.OrganizationId, s.StaffKind })
                .HasPrincipalKey(o => new { o.Id, o.Type })
                .OnDelete(DeleteBehavior.Restrict);

            // Who approved the account.
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.ToTable(t => t.HasCheckConstraint(
                "CK_StaffProfiles_StaffKind",
                "[StaffKind] IN ('Hospital', 'BloodBank')"));
        }
    }
}
