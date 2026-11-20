using BloodFlow.Domain.Entities;
using BloodFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodFlow.Infrastructure.Persistence.Configurations
{
    internal class BloodBatchConfiguration : IEntityTypeConfiguration<BloodBatch>
    {
        public void Configure(EntityTypeBuilder<BloodBatch> builder)
        {
            builder.HasKey(b => b.Id);

            // Computed in C# from OnHand, Reserved and Status; not a column.
            builder.Ignore(b => b.AvailableQuantity);

            builder.Property(b => b.SourceType).HasConversion<string>().HasMaxLength(15).IsUnicode(false);
            builder.Property(b => b.ExpiryBasis).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
            builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(10).IsUnicode(false);

            // DateOnly → SQL Server "date" (no time part).
            builder.Property(b => b.OriginalCollectionDate).HasColumnType("date");
            builder.Property(b => b.ExpiryDate).HasColumnType("date");

            builder.Property(b => b.RowVersion).IsRowVersion();

            builder.HasOne(b => b.Organization)
                .WithMany()
                .HasForeignKey(b => b.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(b => b.BloodGroup)
                .WithMany()
                .HasForeignKey(b => b.BloodGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(b => b.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // FEFO lookups: "usable batches of blood group X at organization Y, earliest expiry first".
            builder.HasIndex(b => new { b.OrganizationId, b.BloodGroupId, b.Status, b.ExpiryDate });

            // The daily expiry job: "Active batches whose expiry date has passed".
            builder.HasIndex(b => new { b.Status, b.ExpiryDate });

            builder.ToTable(t =>
            {
                // The stock invariants from the design. Even a bug in C# cannot break them.
                t.HasCheckConstraint("CK_BloodBatches_OnHand", "[OnHandQuantity] >= 0");
                t.HasCheckConstraint("CK_BloodBatches_Reserved", "[ReservedQuantity] >= 0");
                t.HasCheckConstraint("CK_BloodBatches_OnHandCoversReserved", "[OnHandQuantity] >= [ReservedQuantity]");
                t.HasCheckConstraint("CK_BloodBatches_Initial", "[InitialQuantity] > 0");
                t.HasCheckConstraint("CK_BloodBatches_Dates", "[ExpiryDate] > [OriginalCollectionDate]");
                t.HasCheckConstraint("CK_BloodBatches_Status", "[Status] IN ('Active', 'Expired')");
                t.HasCheckConstraint("CK_BloodBatches_SourceType", "[SourceType] IN ('ManualIntake', 'Donation', 'Transfer')");
                t.HasCheckConstraint("CK_BloodBatches_ExpiryBasis", "[ExpiryBasis] IN ('Entered', 'DefaultShelfLife')");
            });
        }
    }
}
