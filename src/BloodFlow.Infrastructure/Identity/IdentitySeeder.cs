using Microsoft.AspNetCore.Identity;
using BloodFlow.Application.Common;
using Microsoft.Extensions.DependencyInjection;
namespace BloodFlow.Infrastructure.Identity
{
    public static class IdentitySeeder
    {


        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            await SeedRolesAsync(roleManager);

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
                        throw new InvalidOperationException($"Failed to create role: {role},{errors}");
                    }

                }

            }

        }
    }
}
