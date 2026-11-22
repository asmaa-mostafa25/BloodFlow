using BloodFlow.Domain.Entities;
using BloodFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodFlow.Infrastructure.Persistence.Configurations
{
    internal class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
    {
        public void Configure(EntityTypeBuilder<Reservation> builder)
        {
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
            builder.Property(r => r.RowVersion).IsRowVersion();

            // Composite FK (RequestId, BloodGroupId) → BloodRequests(Id, BloodGroupId):
            // a reservation always has the same blood group as its request.
            builder.HasOne(r => r.Request)
                .WithMany(q => q.Reservations)
                .HasForeignKey(r => new { r.RequestId, r.BloodGroupId })
                .HasPrincipalKey(q => new { q.Id, q.BloodGroupId })
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(r => r.Organization)
                .WithMany()
                .HasForeignKey(r => r.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

            // At most ONE active-or-consumed reservation per request (a released one does not count).
            // Even if two blood banks press "approve" at the same moment, only one can win.
            builder.HasIndex(r => r.RequestId)
                .IsUnique()
                .HasFilter("[Status] IN ('Active', 'Consumed')")
                .HasDatabaseName("UX_Reservations_OneLivePerRequest");

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Reservations_Status", "[Status] IN ('Active', 'Consumed', 'Released')");
                t.HasCheckConstraint("CK_Reservations_ClosedAt",
                    "([Status] = 'Active' AND [ClosedAt] IS NULL) OR ([Status] <> 'Active' AND [ClosedAt] IS NOT NULL)");
            });
        }
    }

    internal class ReservationLineConfiguration : IEntityTypeConfiguration<ReservationLine>
    {
        public void Configure(EntityTypeBuilder<ReservationLine> builder)
        {
            builder.HasKey(l => l.Id);

            builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(12).IsUnicode(false);
            builder.Property(l => l.InvalidationReason).HasMaxLength(30).IsUnicode(false);
            builder.Property(l => l.RowVersion).IsRowVersion();

            // Composite FK → Reservations(Id, OrganizationId, BloodGroupId)
            builder.HasOne(l => l.Reservation)
                .WithMany(r => r.Lines)
                .HasForeignKey(l => new { l.ReservationId, l.OrganizationId, l.BloodGroupId })
                .HasPrincipalKey(r => new { r.Id, r.OrganizationId, r.BloodGroupId })
                .OnDelete(DeleteBehavior.Restrict);

            // Composite FK → BloodBatches(Id, OrganizationId, BloodGroupId).
            // Together the two FKs guarantee: the batch is at the SAME organization and has the
            // SAME blood group as the reservation. A wrong-group allocation is impossible.
            builder.HasOne(l => l.Batch)
                .WithMany()
                .HasForeignKey(l => new { l.BatchId, l.OrganizationId, l.BloodGroupId })
                .HasPrincipalKey(b => new { b.Id, b.OrganizationId, b.BloodGroupId })
                .OnDelete(DeleteBehavior.Restrict);

            // "Which active reservations use this batch?" (expiry job, fulfilment).
            builder.HasIndex(l => new { l.BatchId, l.Status });

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ReservationLines_Quantity", "[Quantity] > 0");
                t.HasCheckConstraint("CK_ReservationLines_Status",
                    "[Status] IN ('Active', 'Consumed', 'Released', 'Invalidated')");
                t.HasCheckConstraint("CK_ReservationLines_Invalidation",
                    "([Status] = 'Invalidated' AND [InvalidationReason] IS NOT NULL) OR ([Status] <> 'Invalidated' AND [InvalidationReason] IS NULL)");
            });
        }
    }

    internal class EmergencyOverrideConfiguration : IEntityTypeConfiguration<EmergencyOverride>
    {
        public void Configure(EntityTypeBuilder<EmergencyOverride> builder)
        {
            builder.HasKey(o => o.Id);

            builder.Property(o => o.RequestType).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
            builder.Property(o => o.ReviewStatus).HasConversion<string>().HasMaxLength(15).IsUnicode(false);
            builder.Property(o => o.ReasonText).IsRequired().HasMaxLength(500);
            builder.Property(o => o.ReviewNotes).HasMaxLength(500);

            // Composite FK (RequestId, RequestType) → BloodRequests(Id, RequestType) with RequestType
            // fixed to 'Emergency': only an emergency request can ever be overridden.
            builder.HasOne(o => o.Request)
                .WithMany()
                .HasForeignKey(o => new { o.RequestId, o.RequestType })
                .HasPrincipalKey(r => new { r.Id, r.RequestType })
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(o => o.RequestId).IsUnique();
            builder.HasIndex(o => o.ReviewStatus);

            builder.HasOne<Organization>().WithMany().HasForeignKey(o => o.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(o => o.OverriddenByUserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(o => o.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_EmergencyOverrides_Type", "[RequestType] = 'Emergency'");
                t.HasCheckConstraint("CK_EmergencyOverrides_Review", "[ReviewStatus] IN ('PendingReview', 'Reviewed')");
            });
        }
    }
}
