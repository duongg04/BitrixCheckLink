using BitrixChecker.Models;
using BitrixChecker.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BitrixChecker.Services
{
    public class IdentitySeedService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<SeedAdminOptions> seedAdmin)
    {
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly RoleManager<ApplicationRole> _roleManager = roleManager;
        private readonly SeedAdminOptions _seedAdmin = seedAdmin.Value;

        public async Task InitializeAsync()
        {
            foreach (var roleName in new[] { "Admin", "User" })
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    var roleResult = await _roleManager.CreateAsync(new ApplicationRole { Name = roleName, NormalizedName = roleName.ToUpperInvariant() });
                    if (!roleResult.Succeeded && !await _roleManager.RoleExistsAsync(roleName))
                        throw new InvalidOperationException("Unable to seed role " + roleName + ": " + string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }

            // Seeding is opt-in. Production must provide all three values via environment variables.
            if (string.IsNullOrWhiteSpace(_seedAdmin.UserName) && string.IsNullOrWhiteSpace(_seedAdmin.Email) && string.IsNullOrWhiteSpace(_seedAdmin.Password))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_seedAdmin.UserName) || string.IsNullOrWhiteSpace(_seedAdmin.Email) || string.IsNullOrWhiteSpace(_seedAdmin.Password))
            {
                throw new InvalidOperationException("SeedAdmin requires UserName, Email and Password when enabled.");
            }

            var userName = _seedAdmin.UserName;
            var email = _seedAdmin.Email;
            var password = _seedAdmin.Password;

            var adminUser = await _userManager.FindByNameAsync(userName);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = userName,
                    Email = email,
                    DisplayName = "System Administrator",
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(adminUser, password);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException("Unable to seed admin user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            if (!await _userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                var roleResult = await _userManager.AddToRoleAsync(adminUser, "Admin");
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException("Unable to assign Admin role: " + string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }

        }
    }
}
