using Microsoft.EntityFrameworkCore;
using BloodFlow.Domain.Entities;
using BloodFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace BloodFlow.Infrastructure.Persistence
{
 public class AppDbContext: IdentityDbContext<ApplicationUser,IdentityRole<int>, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.UseCollation("Arabic_100_CI_AI");

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
        public DbSet<BloodGroup> BloodGroups { get; set; } = null!;
        public DbSet<Governorate> Governorates { get; set; } = null!;
        public DbSet<Organization> Organizations { get; set; } = null!;
    }
    
}
