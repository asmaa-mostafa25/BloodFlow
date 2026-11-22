using BloodFlow.Domain.Entities;
using BloodFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodFlow.Infrastructure.Persistence.Configurations
{
    internal class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
    {
        public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
        {
            // bigint identity: an ever-growing ledger.
            builder.HasKey(t => t.Id);

            builder.Property(t => t.TransactionType).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
            builder.Property(t => t.ReasonCode).HasConversion<string>().HasMaxLength(12).IsUnicode(false);
            builder.Property(t => t.ActorType).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
            builder.Property(t => t.Reason).HasMaxLength(500);

            builder.HasOne(t => t.Batch)
                .WithMany(b => b.Transactions)
                .HasForeignKey(t => t.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(t => t.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.PerformedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(t => new { t.OrganizationId, t.CreatedAt });

            // Idempotency: the same client request id can only ever be used once.
            builder.HasIndex(t => t.ClientRequestId)
                .IsUnique()
                .HasFilter("[ClientRequestId] IS NOT NULL");

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_InventoryTransactions_Type",
                    "[TransactionType] IN ('Intake', 'Adjust', 'Reserve', 'Release', 'Deduct')");
                t.HasCheckConstraint("CK_InventoryTransactions_ReasonCode",
                    "[ReasonCode] IS NULL OR [ReasonCode] IN ('Damaged', 'Expired', 'Correction')");
                // An adjustment must say why (workflow B1).
                t.HasCheckConstraint("CK_InventoryTransactions_AdjustHasReason",
                    "[TransactionType] <> 'Adjust' OR ([ReasonCode] IS NOT NULL AND [Reason] IS NOT NULL)");
                t.HasCheckConstraint("CK_InventoryTransactions_After",
                    "[OnHandAfter] >= 0 AND [ReservedAfter] >= 0 AND [OnHandAfter] >= [ReservedAfter]");
                t.HasCheckConstraint("CK_InventoryTransactions_ActorType", "[ActorType] IN ('User', 'System')");
                t.HasCheckConstraint("CK_InventoryTransactions_Actor",
                    "([ActorType] = 'User' AND [PerformedByUserId] IS NOT NULL) OR ([ActorType] = 'System' AND [PerformedByUserId] IS NULL)");
            });
        }
    }
}
