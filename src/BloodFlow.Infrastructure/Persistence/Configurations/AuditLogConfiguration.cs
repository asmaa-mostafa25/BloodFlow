using BloodFlow.Domain.Entities;
using BloodFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodFlow.Infrastructure.Persistence.Configurations
{
    internal class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            // bigint identity: an ever-growing ledger.
            builder.HasKey(a => a.Id);

            builder.Property(a => a.ActorType)
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsUnicode(false);

            builder.Property(a => a.ActorRole).HasMaxLength(30).IsUnicode(false);
            builder.Property(a => a.Action).IsRequired().HasMaxLength(60).IsUnicode(false);
            builder.Property(a => a.EntityType).IsRequired().HasMaxLength(60).IsUnicode(false);
            builder.Property(a => a.EntityId).HasMaxLength(40).IsUnicode(false);

            // JSON snapshots can be longer than a few hundred characters.
            builder.Property(a => a.OldValues).HasColumnType("nvarchar(max)");
            builder.Property(a => a.NewValues).HasColumnType("nvarchar(max)");

            builder.Property(a => a.Reason).HasMaxLength(500);

            // Max length of an IPv6 address in text form.
            builder.Property(a => a.IpAddress).HasMaxLength(45).IsUnicode(false);

            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(a => a.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(a => a.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(a => a.OccurredAt);
            builder.HasIndex(a => new { a.EntityType, a.EntityId });

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_AuditLogs_ActorType", "[ActorType] IN ('User', 'System')");
                // A user action must say which user; a system action has no user.
                t.HasCheckConstraint("CK_AuditLogs_Actor",
                    "([ActorType] = 'User' AND [ActorUserId] IS NOT NULL) OR ([ActorType] = 'System' AND [ActorUserId] IS NULL)");
            });
        }
    }
}
