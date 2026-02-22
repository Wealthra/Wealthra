using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Wealthra.Domain.Enums;
using Wealthra.Infrastructure.Identity.Models;

namespace Wealthra.Infrastructure.Persistence.Seeding
{
    public static class IdentitySeeder
    {
        public static async Task SeedDefaultUsersAndRolesAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Seed Roles based on your Domain Enum
            var roles = new[] { Roles.Admin.ToString(), Roles.Basic.ToString() };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Seed Admin User
            var adminEmail = "admin@wealthra.local";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "System",
                    LastName = "Admin",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, "AdminPassword123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, Roles.Admin.ToString());
                }
            }

            // 3. Seed Normal User
            var normalEmail = "user@wealthra.local";
            if (await userManager.FindByEmailAsync(normalEmail) == null)
            {
                var normalUser = new ApplicationUser
                {
                    UserName = normalEmail,
                    Email = normalEmail,
                    FirstName = "Standard",
                    LastName = "User",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(normalUser, "UserPassword123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(normalUser, Roles.Basic.ToString());
                }
            }
        }
    }
}