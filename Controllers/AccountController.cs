using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using HakaTech.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

/// <summary>
/// Käyttäjätilien controller: kirjautuminen, uloskirjautuminen, rekisteröinti
/// (vain admin), salasanan vaihto, kielen valinta ja käyttöoikeuden eston sivu.
/// Tärkeää: kirjautumissivulla on rate-limit-suoja brute-forcea vastaan.
/// </summary>
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

    /// <summary>
    /// GET /Account/Login — Näyttää kirjautumislomakkeen. Jos käyttäjä
    /// on jo kirjautunut, ohjataan suoraan etusivulle.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    /// <summary>
    /// POST /Account/Login — Varsinainen kirjautumisen käsittely.
    /// "auth"-rate-limit suojaa brute-force-yrityksiltä.
    /// Epäonnistuneet yritykset lukitsevat tilin tilapäisesti (Identity).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
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
            var signedInUser = await _userManager.FindByEmailAsync(model.Email);
            _logger.LogInformation("Käyttäjä {UserId} kirjautui sisään.", signedInUser?.Id);
            await _audit.LogAsync("Login", details: model.Email);

            // Varmistetaan että ReturnUrl on paikallinen — estää avoimen ohjauksen
            // (open redirect) -hyökkäykset, joissa hyökkääjä voisi ohjata käyttäjän
            // ulkopuoliselle haitalliselle sivustolle kirjautumisen jälkeen.
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Käyttäjätili on lukittu (email-hash: {Hash}).", model.Email?.GetHashCode());
            await _audit.LogAsync("LoginLockedOut", details: model.Email);
            ModelState.AddModelError(string.Empty,
                "Tili on lukittu liian monen epäonnistuneen kirjautumisyrityksen vuoksi Yritä myöhemmin uudelleen.");
        }
        else
        {
            await _audit.LogAsync("LoginFailed", details: model.Email);
            ModelState.AddModelError(string.Empty, "Virheellinen sähköpostiosoite tai salasana.");
        }

        return View(model);
    }

    /// <summary>POST /Account/Logout — Kirjautuu ulos ja merkitsee tapahtuman audit-lokiin.</summary>
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

    /// <summary>GET /Account/Register — Adminin lomake uuden käyttäjän luomiseen.</summary>
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

    /// <summary>
    /// POST /Account/Register — Luo uuden käyttäjän adminin pyynnöstä.
    /// Vain admin saa kutsua. Asiakaskäyttäjälle valittu yritys on pakollinen.
    /// </summary>
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
            _logger.LogInformation("Admin loi uuden käyttäjän {UserId} roolilla {Role}.", user.Id, model.Role);
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

    /// <summary>GET /Account/ChangePassword — Salasanan vaihtolomake.</summary>
    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    /// <summary>
    /// POST /Account/ChangePassword — Vaihtaa salasanan jos vanha salasana
    /// täsmää. RefreshSignInAsync uudistaa istuntoevästeen, jotta käyttäjä
    /// ei kirjaudu ulos vaihdon yhteydessä.
    /// </summary>
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
            _logger.LogInformation("Käyttäjä {UserId} vaihtoi salasanansa.", user.Id);
            await _audit.LogAsync("PasswordChanged", "User", user.Id);
            TempData["SuccessMessage"] = "Salasana vaihdettu onnistuneesti.";
            return RedirectToAction(nameof(ChangePassword));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    /// <summary>GET /Account/AccessDenied — Sivu johon ohjataan jos käyttöoikeus puuttuu.</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    /// <summary>
    /// POST /Account/SetLanguage — Tallentaa käyttäjän valitseman kielen
    /// evästeeseen, jonka ASP.NET Core lukee jokaisella pyynnöllä.
    /// </summary>
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

    /// <summary>Rakentaa pudotusvalikon vaihtoehdot (aktiiviset asiakkaat aakkosjärjestyksessä).</summary>
    private async Task<IEnumerable<SelectListItem>> BuildCustomerOptions() =>
        (await _db.Customers
            .Where(c => c.IsActive)
            .OrderBy(c => c.CompanyName)
            .ToListAsync())
        .Select(c => new SelectListItem(c.CompanyName, c.Id.ToString()));
}
