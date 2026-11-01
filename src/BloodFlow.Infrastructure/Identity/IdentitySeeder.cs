using BloodFlow.Application.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BloodFlow.Infrastructure.Identity
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            await SeedRolesAsync(roleManager);
            await SeedAdminAsync(userManager, configuration);
        }

        public static async Task SeedRolesAsync(RoleManager<IdentityRole<int>> roleManager)
        {
            foreach (var role in Roles.AllRoles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var result = await roleManager.CreateAsync(new IdentityRole<int>(role));
                    if (!result.Succeeded)
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        throw new InvalidOperationException($"Failed to create role '{role}': {errors}");
                    }
                }
            }
        }

        public static async Task SeedAdminAsync(UserManager<ApplicationUser> userManager, IConfiguration configuration)
        {
            var adminEmail = configuration["AdminSeed:Email"];
            var adminPassword = configuration["AdminSeed:Password"];

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                throw new InvalidOperationException(
                    "AdminSeed:Email and AdminSeed:Password must be set in user secrets.");
            }

            // 1. Look for the admin (may be null)
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            // 2. Create the admin only if not found
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to create admin user: {errors}");
                }
            }

            // 3. Here adminUser always exists (old or new) → make sure it has the role
            if (!await userManager.IsInRoleAsync(adminUser, Roles.SystemAdmin))
            {
                var roleResult = await userManager.AddToRoleAsync(adminUser, Roles.SystemAdmin);
                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to assign admin role: {errors}");
                }
            }
        }
    }
}