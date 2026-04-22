using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HakaTech.Portal.ViewComponents;

public sealed class SidebarViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _users;

    public SidebarViewComponent(UserManager<ApplicationUser> users)
    {
        _users = users;
    }

    public sealed class SidebarVm
    {
        public string? Controller { get; init; }
        public string? Action { get; init; }
        public bool IsAuthenticated { get; init; }
        public bool IsAdmin { get; init; }
        public bool IsCustomerAdmin { get; init; }
        public string? UserName { get; init; }
        public string? CurrentCulture { get; init; }
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var routeValues = ViewContext.RouteData.Values;
        var isAuth = (User as System.Security.Claims.ClaimsPrincipal)?.Identity?.IsAuthenticated == true;
        var isAdmin = User.IsInRole("Admin");
        var isCustomerAdmin = false;
        string? userName = null;

        if (isAuth)
        {
            var principal = HttpContext.User;
            userName = principal.Identity?.Name;
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
