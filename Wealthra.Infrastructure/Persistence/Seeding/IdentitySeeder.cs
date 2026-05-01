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

            // 1. Seed roles
            var roles = new[]
            {
                Roles.SuperAdmin.ToString(),
                Roles.Admin.ToString(),
                Roles.Support.ToString(),
                Roles.Finance.ToString(),
                Roles.Basic.ToString()
            };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Seed Admin User (SuperAdmin + legacy Admin)
            var adminEmail = "admin@wealthra.local";
            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
            if (existingAdmin == null)
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
                    await userManager.AddToRoleAsync(adminUser, Roles.SuperAdmin.ToString());
                    await userManager.AddToRoleAsync(adminUser, Roles.Admin.ToString());
                }
            }
            else
            {
                await EnsureRoleAsync(userManager, existingAdmin, Roles.SuperAdmin.ToString());
                await EnsureRoleAsync(userManager, existingAdmin, Roles.Admin.ToString());
            }

            // 3. Seed Normal User
            await CreateUserIfNotExists(userManager, "user@wealthra.local", "Standard", "User", "UserPassword123!");

            // 4. Seed Anomalous User (for testing alerts/spikes)
            await CreateUserIfNotExists(userManager, "anomalous.user@wealthra.local", "Anomalous", "Tester", "UserPassword123!");

            // 5. Seed Stable User (for testing healthy balance/no alerts)
            await CreateUserIfNotExists(userManager, "stable.user@wealthra.local", "Stable", "Saver", "UserPassword123!");
        }

        private static async Task EnsureRoleAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string role)
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }

        private static async Task CreateUserIfNotExists(UserManager<ApplicationUser> userManager, string email, string firstName, string lastName, string password)
        {
            if (await userManager.FindByEmailAsync(email) == null)
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, Roles.Basic.ToString());
                }
            }
        }
    }
}