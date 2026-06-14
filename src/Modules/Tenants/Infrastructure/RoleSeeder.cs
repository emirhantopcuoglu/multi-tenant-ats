using Ats.Modules.Tenants.Domain;
using Microsoft.AspNetCore.Identity;

namespace Ats.Modules.Tenants.Infrastructure;

public static class RoleSeeder
{
    public static async Task SeedAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }
    }
}
