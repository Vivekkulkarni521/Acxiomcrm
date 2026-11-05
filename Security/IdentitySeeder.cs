using ACXIOMCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ACXIOMCRM.Security
{
    public static class IdentitySeeder
    {
        private static readonly string[] Roles = { "Admin", "Manager", "SalesExecutive" };

        public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

            var legacyUsers = await userManager.Users.Where(u => u.DisplayName == null || u.DisplayName == "").ToListAsync();
            foreach (var legacyUser in legacyUsers)
            {
                legacyUser.DisplayName = legacyUser.Email ?? legacyUser.UserName ?? "User";
                var profileResult = await userManager.UpdateAsync(legacyUser);
                if (!profileResult.Succeeded)
                    throw new InvalidOperationException("Unable to initialize a legacy user profile: " + string.Join(", ", profileResult.Errors.Select(e => e.Code)));
            }

            foreach (var roleName in Roles)
            {
                if (await roleManager.RoleExistsAsync(roleName))
                    continue;

                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Unable to create Identity role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Code))}");
            }

            await SeedDefaultPermissionsAsync(roleManager, "Manager", CrmPermissionCatalog.ManagerDefaults);
            await SeedDefaultPermissionsAsync(roleManager, "SalesExecutive", CrmPermissionCatalog.SalesExecutiveDefaults);

            var email = configuration["BootstrapAdmin:Email"]?.Trim();
            var password = configuration["BootstrapAdmin:Password"];
            var name = configuration["BootstrapAdmin:Name"]?.Trim();
            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrEmpty(password))
            {
                var existingAdmins = await userManager.GetUsersInRoleAsync("Admin");
                if (existingAdmins.Any(user => user.IsActive))
                {
                    logger.LogInformation("Using the existing active Admin account; no bootstrap credentials were supplied.");
                    return;
                }

                throw new InvalidOperationException("No active Admin account exists. Configure BootstrapAdmin__Email and BootstrapAdmin__Password through a secure secret store or environment variables before starting this CRM database.");
            }
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
                throw new InvalidOperationException("Configure both BootstrapAdmin:Email and BootstrapAdmin:Password, or configure neither.");

            var admin = await userManager.FindByEmailAsync(email);
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    DisplayName = string.IsNullOrWhiteSpace(name) ? "Administrator" : name,
                    IsActive = true
                };
                var createResult = await userManager.CreateAsync(admin, password);
                if (!createResult.Succeeded)
                    throw new InvalidOperationException("Unable to create bootstrap Admin: " + string.Join(", ", createResult.Errors.Select(e => e.Code)));
            }
            else if (!admin.IsActive)
            {
                admin.IsActive = true;
                var activateResult = await userManager.UpdateAsync(admin);
                if (!activateResult.Succeeded)
                    throw new InvalidOperationException("Unable to activate the configured bootstrap Admin account: " + string.Join(", ", activateResult.Errors.Select(e => e.Code)));
                await userManager.UpdateSecurityStampAsync(admin);
            }

            if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                var roleResult = await userManager.AddToRoleAsync(admin, "Admin");
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException("Unable to grant the bootstrap account the Admin role: " + string.Join(", ", roleResult.Errors.Select(e => e.Code)));

                var stampResult = await userManager.UpdateSecurityStampAsync(admin);
                if (!stampResult.Succeeded)
                    throw new InvalidOperationException("Unable to refresh the bootstrap Admin security stamp: " + string.Join(", ", stampResult.Errors.Select(e => e.Code)));
            }
        }

        private static async Task SeedDefaultPermissionsAsync(RoleManager<IdentityRole> roleManager, string roleName, IEnumerable<string> permissions)
        {
            var role = await roleManager.FindByNameAsync(roleName)
                ?? throw new InvalidOperationException($"Identity role '{roleName}' was not found after seeding.");
            var claims = await roleManager.GetClaimsAsync(role);
            if (claims.Any(claim => claim.Type == CrmPermissionCatalog.ClaimType))
                return;

            foreach (var permission in permissions)
            {
                var result = await roleManager.AddClaimAsync(role, new System.Security.Claims.Claim(CrmPermissionCatalog.ClaimType, permission));
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Unable to seed '{permission}' permission for '{roleName}': {string.Join(", ", result.Errors.Select(error => error.Code))}");
            }
        }
    }
}
