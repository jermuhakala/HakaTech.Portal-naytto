using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HakaTech.Portal.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser>   _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController>     _logger;

    public AccountController(
        UserManager<ApplicationUser>   userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController>     logger)
    {
        _userManager   = userManager;
        _signInManager = signInManager;
        _logger        = logger;
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

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Käyttäjätili {Email} on lukittu.", model.Email);
            ModelState.AddModelError(string.Empty,
                "Tili on lukittu liian monien epäonnistuneiden kirjautumisyritysten vuoksi. Yritä myöhemmin uudelleen.");
        }
        else
        {
            ModelState.AddModelError(string.Empty,
                "Virheellinen sähköpostiosoite tai salasana.");
        }

        return View(model);
    }

    // ── POST /Account/Logout ────────────────────────────────────────
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("Käyttäjä kirjautui ulos.");
        return RedirectToAction("Login", "Account");
    }

    // ── GET /Account/Register ───────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    // ── POST /Account/Register ──────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email    = model.Email,
            FullName = model.FullName
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            _logger.LogInformation("Admin loi uuden käyttäjän {Email}.", model.Email);
            TempData["SuccessMessage"] = $"Käyttäjä {model.Email} luotu onnistuneesti.";
            return RedirectToAction(nameof(Register));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

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
            // Päivitetään kirjautumistunnus niin, ettei sessio vanhene
            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("Käyttäjä {Email} vaihtoi salasanansa.", user.Email);
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
}
