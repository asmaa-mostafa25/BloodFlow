using System;
using BloodFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace BloodFlow.Infrastructure.Persistence.Configurations
{
    internal class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
    {
        public void Configure(EntityTypeBuilder<Organization> builder)
        {
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Name).IsRequired().HasMaxLength(200);
            builder.Property(o => o.CityName).IsRequired().HasMaxLength(100);
          
            builder.Property(o => o.RowVersion).IsRowVersion();
            builder.HasOne(o => o.Governorate)
                   .WithMany(g => g.Organizations)
                   .HasForeignKey(o => o.GovernorateId)
                   .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(o=> new { o.Name, o.GovernorateId }).IsUnique();

            builder.Property(o => o.Status)
                .HasConversion<string>()
                .HasMaxLength(12)
                .IsUnicode(false);
            builder.Property(o => o.Type)
                .HasConversion<string>()
                .HasMaxLength(12)
                .IsUnicode(false);
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Organizations_Status", "[Status] IN ('Active', 'Deactivated')");

                t.HasCheckConstraint("CK_Organizations_Type", "[Type] IN ('Hospital', 'BloodBank')");
            }
            );
        }
    }
}
