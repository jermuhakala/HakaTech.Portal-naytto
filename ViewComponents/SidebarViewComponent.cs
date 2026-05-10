using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HakaTech.Portal.ViewComponents;

/// <summary>
/// Vasemman sivupalkin (sidebar) ViewComponent. Selvittää nykyisen
/// käyttäjän roolin ja aktiivisen sivun, jotta valikossa korostetaan
/// oikea kohta ja näytetään vain käyttäjälle sallitut linkit.
/// </summary>
public sealed class SidebarViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _users;

    public SidebarViewComponent(UserManager<ApplicationUser> users)
    {
        _users = users;
    }

    /// <summary>Sivupalkin näkymälle annettava data.</summary>
    public sealed class SidebarVm
    {
        /// <summary>Nykyisen pyynnön controller (esim. "Ticket") — käytetään aktiivisen linkin korostamiseen.</summary>
        public string? Controller { get; init; }

        /// <summary>Nykyisen pyynnön action.</summary>
        public string? Action { get; init; }

        public bool IsAuthenticated { get; init; }
        public bool IsAdmin { get; init; }

        /// <summary>True jos käyttäjä on oman yrityksensä pääkäyttäjä — saa näkyviin "Käyttäjät"-linkin.</summary>
        public bool IsCustomerAdmin { get; init; }

        public string? UserName { get; init; }

        /// <summary>Nykyinen kieli (fi-FI / sv-SE / en-US) — kielivalitsimen tila.</summary>
        public string? CurrentCulture { get; init; }
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        // Selvitetään nykyisen reitin tiedot ja käyttäjän rooli.
        var routeValues = ViewContext.RouteData.Values;
        var isAuth = (User as System.Security.Claims.ClaimsPrincipal)?.Identity?.IsAuthenticated == true;
        var isAdmin = User.IsInRole("Admin");
        var isCustomerAdmin = false;
        string? userName = null;

        if (isAuth)
        {
            var principal = HttpContext.User;
            userName = principal.Identity?.Name;

            // Customer-admin -lippu vaatii tietokantakyselyn — tehdään vain
            // jos käyttäjä ei ole jo admin (admin näkee kaiken muutenkin).
            if (!isAdmin)
            {
                var me = await _users.GetUserAsync(principal);
                isCustomerAdmin = me?.IsCustomerAdmin == true;
            }
        }

        var vm = new SidebarVm
        {
            Controller = routeValues["controller"]?.ToString(),
            Action = routeValues["action"]?.ToString(),
            IsAuthenticated = isAuth,
            IsAdmin = isAdmin,
            IsCustomerAdmin = isCustomerAdmin,
            UserName = userName,
            CurrentCulture = System.Globalization.CultureInfo.CurrentUICulture.Name
        };

        return View(vm);
    }
}
