using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodFlow.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Extra columns on AspNetUsers. Runs after IdentityDbContext's own configuration
    /// (base.OnModelCreating), so it only adds to / overrides what Identity set up.
    /// </summary>
    internal class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
    {
        public void Configure(EntityTypeBuilder<ApplicationUser> builder)
        {
            builder.Property(u => u.FullName)
                .IsRequired()
                .HasMaxLength(200);

            // Stored as text ('Pending', 'Active', ...) like every other status in BloodFlow.
            // The default fills existing rows when the column is added; the admin seeder
            // then switches the seeded admin to Active.
            builder.Property(u => u.AccountStatus)
                .HasConversion<string>()
                .HasMaxLength(12)
                .IsUnicode(false)
                .HasDefaultValue(AccountStatus.Pending);

            builder.Property(u => u.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            // Identity creates a NON-unique index on NormalizedEmail. RequireUniqueEmail is only
            // checked in code, so we make the database enforce it too (last line of defense).
            builder.HasIndex(u => u.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .IsUnique()
                .HasFilter("[NormalizedEmail] IS NOT NULL");

            builder.ToTable(t => t.HasCheckConstraint(
                "CK_AspNetUsers_AccountStatus",
                "[AccountStatus] IN ('Pending', 'Active', 'Rejected', 'Deactivated')"));
        }
    }
}
