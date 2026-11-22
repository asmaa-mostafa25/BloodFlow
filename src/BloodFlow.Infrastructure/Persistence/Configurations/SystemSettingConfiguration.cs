using BloodFlow.Domain.Entities;
using BloodFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodFlow.Infrastructure.Persistence.Configurations
{
    internal class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
    {
        public void Configure(EntityTypeBuilder<SystemSetting> builder)
        {
            builder.HasKey(s => s.SettingKey);

            builder.Property(s => s.SettingKey)
                .HasMaxLength(60)
                .IsUnicode(false);

            builder.Property(s => s.SettingValue)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(s => s.ValueType)
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsUnicode(false);

            builder.Property(s => s.Description)
                .IsRequired()
                .HasMaxLength(300);

            builder.Property(s => s.ApprovalReference)
                .HasMaxLength(200);

            builder.Property(s => s.RowVersion).IsRowVersion();

            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.ToTable(t => t.HasCheckConstraint(
                "CK_SystemSettings_ValueType",
                "[ValueType] IN ('Int', 'Decimal', 'Bool', 'String')"));
        }
    }
}
