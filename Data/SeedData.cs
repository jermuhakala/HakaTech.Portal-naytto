using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Identity;

namespace HakaTech.Portal.Data;

public static class SeedData
{
    // Roolivakiot – käytetään [Authorize(Roles = Roles.Admin)] attribuuteissa
    public static class Roles
    {
        public const string Admin    = "Admin";
        public const string Customer = "Customer";
    }

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // ── Luo roolit jos puuttuu ───────────────────────────
        foreach (var role in new[] { Roles.Admin, Roles.Customer })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── Luo oletusadmin jos puuttuu ──────────────────────
        const string adminEmail    = "admin@hakatech.fi";
        const string adminPassword = "HakaTech2025!";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email    = adminEmail,
                FullName = "HakaTech Admin",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, Roles.Admin);
        }
    }
}
