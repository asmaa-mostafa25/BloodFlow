using Microsoft.EntityFrameworkCore;
using BloodFlow.Domain.Entities;

namespace BloodFlow.Infrastructure.Persistence
{
 public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
        public DbSet<BloodGroup> BloodGroups { get; set; } = null!;
        public DbSet<Governorate> Governorates { get; set; } = null!;
        public DbSet<Organization> Organizations { get; set; } = null!;
    }
    
}
