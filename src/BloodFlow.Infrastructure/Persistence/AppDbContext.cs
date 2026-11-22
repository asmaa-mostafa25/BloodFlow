using BloodFlow.Domain.Entities;
using BloodFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Must stay first: configures all the Identity tables.
            base.OnModelCreating(modelBuilder);
            modelBuilder.UseCollation("Arabic_100_CI_AI");

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            // Request numbers (BR-2026-000123) come from a database sequence: unique even
            // when two hospitals create requests at the same moment.
            modelBuilder.HasSequence<int>(Configurations.BloodRequestConfiguration.NumberSequence).StartsAt(1);
        }

        public DbSet<BloodGroup> BloodGroups { get; set; } = null!;
        public DbSet<Governorate> Governorates { get; set; } = null!;
        public DbSet<Organization> Organizations { get; set; } = null!;
        public DbSet<StaffProfile> StaffProfiles { get; set; } = null!;
        public DbSet<SystemSetting> SystemSettings { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<BloodBatch> BloodBatches { get; set; } = null!;
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; } = null!;
        public DbSet<BloodRequest> BloodRequests { get; set; } = null!;
        public DbSet<BloodRequestStatusHistory> BloodRequestStatusHistory { get; set; } = null!;
        public DbSet<Reservation> Reservations { get; set; } = null!;
        public DbSet<ReservationLine> ReservationLines { get; set; } = null!;
        public DbSet<EmergencyOverride> EmergencyOverrides { get; set; } = null!;
    }
}
