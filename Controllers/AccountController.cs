using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using HakaTech.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser>   _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController>     _logger;
    private readonly ApplicationDbContext           _db;
    private readonly IAuditService                  _audit;

    public AccountController(
        UserManager<ApplicationUser>   userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController>     logger,
        ApplicationDbContext           db,
        IAuditService                  audit)
    {
        _userManager   = userManager;
        _signInManager = signInManager;
        _logger        = logger;
        _db            = db;
        _audit         = audit;
    }

    // ── GET /Account/Login ──────────────────────────────────────────
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    // ── POST /Account/Login ─────────────────────────────────────────
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("Käyttäjä {Email} kirjautui sisään.", model.Email);
            await _audit.LogAsync("Login", details: model.Email);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Käyttäjätili {Email} on lukittu.", model.Email);
            await _audit.LogAsync("LoginLockedOut", details: model.Email);
            ModelState.AddModelError(string.Empty,
                "Tili on lukittu liian monien epäonnistuneiden kirjautumisyritysten vuoksi. Yritä myöhemmin uudelleen.");
        }
        else
        {
            await _audit.LogAsync("LoginFailed", details: model.Email);
            ModelState.AddModelError(string.Empty, "Virheellinen sähköpostiosoite tai salasana.");
        }

        return View(model);
    }

    // ── POST /Account/Logout ────────────────────────────────────────
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync("Logout");
        await _signInManager.SignOutAsync();
        _logger.LogInformation("Käyttäjä kirjautui ulos.");
        return RedirectToAction("Login", "Account");
    }

    // ── GET /Account/Register ───────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register(int? customerId = null)
    {
        var model = new RegisterViewModel
        {
            CustomerId      = customerId,
            CustomerOptions = await BuildCustomerOptions()
        };
        return View(model);
    }

    // ── POST /Account/Register ──────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.CustomerOptions = await BuildCustomerOptions();
            return View(model);
        }

        // Rooli täytyy olla Admin tai Customer
        if (model.Role != "Admin" && model.Role != "Customer")
            model.Role = "Customer";

        // Asiakaskäyttäjällä pitää olla yritys valittuna
        if (model.Role == "Customer" && model.CustomerId is null)
        {
            ModelState.AddModelError(nameof(model.CustomerId),
                "Asiakaskäyttäjälle on valittava yritys.");
            model.CustomerOptions = await BuildCustomerOptions();
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName   = model.Email,
            Email      = model.Email,
            FullName   = model.FullName,
            CustomerId = model.Role == "Customer" ? model.CustomerId : null
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, model.Role);
            _logger.LogInformation("Admin loi uuden käyttäjän {Email} roolilla {Role}.", model.Email, model.Role);
            await _audit.LogAsync("UserCreated", "User", user.Id, $"{model.Email} / {model.Role}");
            TempData["SuccessMessage"] = $"Käyttäjä {model.Email} luotu onnistuneesti ({model.Role}).";

            // Jos luotiin asiakaskäyttäjä, palaa asiakkaan tietoihin
            if (model.Role == "Customer" && model.CustomerId.HasValue)
                return RedirectToAction("Details", "Customer", new { id = model.CustomerId.Value });

            return RedirectToAction(nameof(Register));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        model.CustomerOptions = await BuildCustomerOptions();
        return View(model);
    }

    // ── GET /Account/ChangePassword ─────────────────────────────────
    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    // ── POST /Account/ChangePassword ────────────────────────────────
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return RedirectToAction(nameof(Login));

        var result = await _userManager.ChangePasswordAsync(
            user, model.CurrentPassword, model.NewPassword);

        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("Käyttäjä {Email} vaihtoi salasanansa.", user.Email);
            await _audit.LogAsync("PasswordChanged", "User", user.Id);
            TempData["SuccessMessage"] = "Salasana vaihdettu onnistuneesti.";
            return RedirectToAction(nameof(ChangePassword));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    // ── GET /Account/AccessDenied ───────────────────────────────────
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // ── POST /Account/SetLanguage ─────────────────────────────────────
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult SetLanguage(string culture, string returnUrl = "/")
    {
        var allowed = new HashSet<string> { "fi-FI", "sv-SE", "en-US" };
        if (!allowed.Contains(culture)) culture = "fi-FI";

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires  = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });

        return LocalRedirect(returnUrl);
    }

    // ── Apumetodit ───────────────────────────────────────────────────
    private async Task<IEnumerable<SelectListItem>> BuildCustomerOptions() =>
        (await _db.Customers
            .Where(c => c.IsActive)
            .OrderBy(c => c.CompanyName)
            .ToListAsync())
        .Select(c => new SelectListItem(c.CompanyName, c.Id.ToString()));
}
