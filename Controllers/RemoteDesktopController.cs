using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using HakaTech.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

[Authorize]
public class RemoteDesktopController : Controller
{
    private readonly ApplicationDbContext             _db;
    private readonly UserManager<ApplicationUser>     _userManager;
    private readonly IGuacamoleService                _guacamole;
    private readonly ILogger<RemoteDesktopController> _logger;

    public RemoteDesktopController(
        ApplicationDbContext             db,
        UserManager<ApplicationUser>     userManager,
        IGuacamoleService                guacamole,
        ILogger<RemoteDesktopController> logger)
    {
        _db          = db;
        _userManager = userManager;
        _guacamole   = guacamole;
        _logger      = logger;
    }

    // ── GET /RemoteDesktop ──────────────────────────────────────────
    // Asiakas: omat yhteydet kortteina. Admin: ohjataan Manageen.
    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Admin"))
            return RedirectToAction(nameof(Manage));

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.CustomerId is null)
            return View(Enumerable.Empty<RemoteDesktopConnectionCardViewModel>());

        var connections = await _db.RemoteDesktopConnections
            .Where(r => r.CustomerId == currentUser.CustomerId && r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new RemoteDesktopConnectionCardViewModel
            {
                Id       = r.Id,
                Name     = r.Name,
                Protocol = r.Protocol,
                Hostname = r.Hostname,
                Port     = r.Port,
                Notes    = r.Notes
            })
            .ToListAsync();

        return View(connections);
    }

    // ── GET /RemoteDesktop/Manage ───────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Manage()
    {
        var connections = await _db.RemoteDesktopConnections
            .Include(r => r.Customer)
            .OrderBy(r => r.Customer!.CompanyName)
            .ThenBy(r => r.Name)
            .ToListAsync();

        return View(connections);
    }

    // ── GET /RemoteDesktop/Create ───────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(int? customerId)
    {
        var model = new RemoteDesktopConnectionFormViewModel
        {
            CustomerId      = customerId ?? 0,
            CustomerOptions = await BuildCustomerOptionsAsync()
        };
        return View(model);
    }

    // ── POST /RemoteDesktop/Create ──────────────────────────────────
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RemoteDesktopConnectionFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.CustomerOptions = await BuildCustomerOptionsAsync();
            return View(model);
        }

        var connection = new RemoteDesktopConnection
        {
            Name                 = model.Name,
            Protocol             = model.Protocol,
            Hostname             = model.Hostname,
            Port                 = model.Port,
            Username             = model.Username,
            IgnoreCert           = model.IgnoreCert,
            Security             = model.Security,
            Notes                = model.Notes,
            IsActive             = model.IsActive,
            CustomerId           = model.CustomerId,
            GuacamoleConnectionId = model.GuacamoleConnectionId,
            CreatedAt            = DateTime.UtcNow,
            EncryptedPassword    = !string.IsNullOrEmpty(model.PlainPassword)
                ? _guacamole.ProtectPassword(model.PlainPassword)
                : null
        };

        _db.RemoteDesktopConnections.Add(connection);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Etätyöpöytäyhteys '{Name}' (Id={Id}) luotu.", connection.Name, connection.Id);
        TempData["SuccessMessage"] = $"Yhteys \"{connection.Name}\" luotu.";
        return RedirectToAction(nameof(Manage));
    }

    // ── GET /RemoteDesktop/Edit/5 ───────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var conn = await _db.RemoteDesktopConnections.FindAsync(id);
        if (conn is null) return NotFound();

        var model = new RemoteDesktopConnectionFormViewModel
        {
            Id                   = conn.Id,
            Name                 = conn.Name,
            Protocol             = conn.Protocol,
            Hostname             = conn.Hostname,
            Port                 = conn.Port,
            Username             = conn.Username,
            IgnoreCert           = conn.IgnoreCert,
            Security             = conn.Security,
            Notes                = conn.Notes,
            IsActive             = conn.IsActive,
            CustomerId           = conn.CustomerId,
            GuacamoleConnectionId = conn.GuacamoleConnectionId,
            CustomerOptions      = await BuildCustomerOptionsAsync()
            // PlainPassword jätetään tyhjäksi – käyttäjä täyttää vain jos haluaa vaihtaa
        };
        return View(model);
    }

    // ── POST /RemoteDesktop/Edit/5 ──────────────────────────────────
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RemoteDesktopConnectionFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.CustomerOptions = await BuildCustomerOptionsAsync();
            return View(model);
        }

        var conn = await _db.RemoteDesktopConnections.FindAsync(id);
        if (conn is null) return NotFound();

        conn.Name                 = model.Name;
        conn.Protocol             = model.Protocol;
        conn.Hostname             = model.Hostname;
        conn.Port                 = model.Port;
        conn.Username             = model.Username;
        conn.IgnoreCert           = model.IgnoreCert;
        conn.Security             = model.Security;
        conn.Notes                = model.Notes;
        conn.IsActive             = model.IsActive;
        conn.CustomerId           = model.CustomerId;
        conn.GuacamoleConnectionId = model.GuacamoleConnectionId;

        if (!string.IsNullOrEmpty(model.PlainPassword))
            conn.EncryptedPassword = _guacamole.ProtectPassword(model.PlainPassword);

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Etätyöpöytäyhteys '{Name}' (Id={Id}) päivitetty.", conn.Name, id);
        TempData["SuccessMessage"] = $"Yhteys \"{conn.Name}\" päivitetty.";
        return RedirectToAction(nameof(Manage));
    }

    // ── POST /RemoteDesktop/Delete/5 ────────────────────────────────
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var conn = await _db.RemoteDesktopConnections.FindAsync(id);
        if (conn is null) return NotFound();

        conn.IsActive = false;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Etätyöpöytäyhteys '{Name}' (Id={Id}) poistettu käytöstä.", conn.Name, id);
        TempData["SuccessMessage"] = $"Yhteys \"{conn.Name}\" poistettu käytöstä.";
        return RedirectToAction(nameof(Manage));
    }

    // ── GET /RemoteDesktop/Connect/5 ────────────────────────────────
    public async Task<IActionResult> Connect(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var conn = await _db.RemoteDesktopConnections
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

        if (conn is null) return NotFound();

        // Asiakaseristys
        if (!isAdmin && conn.CustomerId != currentUser?.CustomerId)
            return Forbid();

        string? url = await _guacamole.BuildConnectionUrlAsync(conn);

        _logger.LogInformation(
            "Käyttäjä {Email} avaa etäyhteyden '{Name}' (Id={Id}).",
            currentUser?.Email ?? "tuntematon", conn.Name, id);

        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("Guacamole-URL on tyhjä yhteydelle '{Name}' (Id={Id}).", conn.Name, id);
            TempData["ErrorMessage"] = "Etäyhteyden muodostaminen epäonnistui. Tarkista yhteyden asetukset.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.ConnectionName = conn.Name;
        ViewBag.GuacamoleUrl   = url;
        return View();
    }

    // ── Apumetodi ────────────────────────────────────────────────────
    private async Task<IEnumerable<SelectListItem>> BuildCustomerOptionsAsync() =>
        (await _db.Customers
            .Where(c => c.IsActive)
            .OrderBy(c => c.CompanyName)
            .ToListAsync())
        .Select(c => new SelectListItem(c.CompanyName, c.Id.ToString()));
}
