// Nimiavaruuksien tuonnit.
using HakaTech.Portal.Data;                        // ApplicationDbContext — tietokantayhteys.
using HakaTech.Portal.Models.Domain;               // RemoteDesktopConnection, ApplicationUser...
using HakaTech.Portal.Models.ViewModels;           // RemoteDesktopConnectionFormViewModel, CardViewModel.
using HakaTech.Portal.Services;                    // IGuacamoleService — selainpohjainen etäyhteys.
using Microsoft.AspNetCore.Authorization;          // [Authorize]-attribuutti.
using Microsoft.AspNetCore.Identity;               // UserManager.
using Microsoft.AspNetCore.Mvc;                    // Controller, IActionResult, TempData...
using Microsoft.AspNetCore.Mvc.Rendering;          // SelectListItem — pudotusvalikon vaihtoehto.
using Microsoft.EntityFrameworkCore;               // Include, ToListAsync, FindAsync...

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Etätyöpöytäyhteyksien controller. Asiakas näkee oman yrityksensä
/// yhteydet ja voi avata ne selaimessa Apache Guacamolen kautta.
/// Admin hallinnoi yhteyksiä (luominen, salasanat).
/// </summary>
// [Authorize] = kirjautuminen vaaditaan kaikkiin toimintoihin.
[Authorize]
public class RemoteDesktopController : Controller
{
    // Tietokantayhteys.
    private readonly ApplicationDbContext             _db;
    // UserManager — haetaan kirjautunut käyttäjä ja CustomerId.
    private readonly UserManager<ApplicationUser>     _userManager;
    // Guacamole-palvelu — rakentaa etäyhteyslinkin ja salaa salasanat.
    // Apache Guacamole = selaimen kautta toimiva etätyöpöytä (RDP/VNC/SSH).
    private readonly IGuacamoleService                _guacamole;
    // Diagnostiikkaloki kehittäjälle.
    private readonly ILogger<RemoteDesktopController> _logger;

    // Konstruktori: DI-säiliö täyttää parametrit.
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

    // ── GET /RemoteDesktop ───────────────────────────────────────────────────
    // Asiakaskäyttäjälle: omat yhteydet kortteina.
    // Adminille: ohjataan hallintanäkymään.
    public async Task<IActionResult> Index()
    {
        // Admin ohjataan Manage-sivulle — hänellä on eri hallintanäkymä.
        if (User.IsInRole("Admin"))
            return RedirectToAction(nameof(Manage));

        var currentUser = await _userManager.GetUserAsync(User);
        // Jos käyttäjällä ei ole CustomerId:tä (ei yritystä), palautetaan tyhjä lista.
        // Enumerable.Empty<T>() = tyhjä kokoelma ilman allokointia.
        if (currentUser?.CustomerId is null)
            return View(Enumerable.Empty<RemoteDesktopConnectionCardViewModel>());

        // Haetaan asiakkaan aktiiviset yhteydet kortteina.
        // Select() muuntaa suoraan SQL-tasolla — ei haeta salasanahashia tai muita arkaluonteisia kenttiä.
        // Tämä on datan minimointiperiaate (GDPR): näytetään vain tarpeellinen tieto.
        var connections = await _db.RemoteDesktopConnections
            .Where(r => r.CustomerId == currentUser.CustomerId // Vain oman yrityksen yhteydet.
                     && r.IsActive)                           // Vain aktiiviset (IsActive=true).
            .OrderBy(r => r.Name) // Aakkosjärjestyksessä.
            .Select(r => new RemoteDesktopConnectionCardViewModel
            {
                Id       = r.Id,
                Name     = r.Name,         // Yhteyden nimi (esim. "Toimistopalvelin").
                Protocol = r.Protocol,     // RDP, VNC tai SSH.
                Hostname = r.Hostname,     // Palvelimen osoite tai IP.
                Port     = r.Port,         // Porttinumero.
                Notes    = r.Notes         // Lisätietoja.
                // Ei: Username, EncryptedPassword — turvallisuussyistä ei lähetetä selaimelle.
            })
            .ToListAsync();

        return View(connections);
    }

    // ── GET /RemoteDesktop/Manage ────────────────────────────────────────────
    // Admin näkee kaikkien asiakkaiden kaikki yhteydet.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Manage()
    {
        // Haetaan kaikki yhteydet asiakkaan tiedoilla — admin tarvitsee tiedon kuka omistaa yhteyden.
        var connections = await _db.RemoteDesktopConnections
            .Include(r => r.Customer) // Yrityksen nimi taulukkoon.
            // Järjestys: ensin yrityksen nimi, sitten yhteyden nimi.
            // "!" = null-forgiving: Customer ei ole null kun Include() on käytetty.
            .OrderBy(r => r.Customer!.CompanyName)
            .ThenBy(r => r.Name)
            .ToListAsync();

        return View(connections);
    }

    // ── GET /RemoteDesktop/Create ────────────────────────────────────────────
    // Tyhjä uuden yhteyden luontilomake.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        int? customerId) // Valinnainen: esivalitaan asiakas jos tultiin asiakassivulta.
    {
        var model = new RemoteDesktopConnectionFormViewModel
        {
            CustomerId      = customerId ?? 0, // 0 = ei valittu.
            CustomerOptions = await BuildCustomerOptionsAsync() // Pudotusvalikon vaihtoehdot.
        };
        return View(model);
    }

    // ── POST /RemoteDesktop/Create ───────────────────────────────────────────
    // Tallentaa uuden etätyöpöytäyhteyden tietokantaan.
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RemoteDesktopConnectionFormViewModel model)
    {
        // Tarkistetaan lomakkeen validointi.
        if (!ModelState.IsValid)
        {
            model.CustomerOptions = await BuildCustomerOptionsAsync();
            return View(model);
        }

        // Luodaan uusi etätyöpöytäyhteysentiteetti.
        var connection = new RemoteDesktopConnection
        {
            Name                  = model.Name,
            Protocol              = model.Protocol,      // RDP, VNC tai SSH (enum).
            Hostname              = model.Hostname,      // Palvelimen osoite.
            Port                  = model.Port,          // Porttinumero.
            Username              = model.Username,      // Käyttäjätunnus.
            IgnoreCert            = model.IgnoreCert,    // Ohita sertifikaattivirhe (itseallekirjoitetut).
            Security              = model.Security,      // Salaustyyppi (RDP-spesifi).
            Notes                 = model.Notes,
            IsActive              = model.IsActive,
            CustomerId            = model.CustomerId,
            GuacamoleConnectionId = model.GuacamoleConnectionId, // Guacamolen oma tunniste (jos hallitaan ulkoisesti).
            CreatedAt             = DateTime.UtcNow,
            // Salataan salasana vain jos se on annettu.
            // ProtectPassword() käyttää ASP.NET Core Data Protection API:a — tuloksena on salattu merkkijono.
            // null = salasanaa ei tallenneta (esim. käytetään SSH-avainta tai Guacamole-integraatiota).
            EncryptedPassword     = !string.IsNullOrEmpty(model.PlainPassword)
                ? _guacamole.ProtectPassword(model.PlainPassword) // Salaa salasana turvallisesti.
                : null
        };

        _db.RemoteDesktopConnections.Add(connection);
        await _db.SaveChangesAsync(); // INSERT SQL.

        _logger.LogInformation(
            "Etätyöpöytäyhteys '{Name}' (Id={Id}) luotu.", connection.Name, connection.Id);
        TempData["SuccessMessage"] = $"Yhteys \"{connection.Name}\" luotu.";
        return RedirectToAction(nameof(Manage));
    }

    // ── GET /RemoteDesktop/Edit/5 ────────────────────────────────────────────
    // Muokkauslomake olemassaolevalle yhteydelle.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var conn = await _db.RemoteDesktopConnections.FindAsync(id);
        if (conn is null) return NotFound();

        // Täytetään ViewModel olemassaolevasta yhteydestä.
        var model = new RemoteDesktopConnectionFormViewModel
        {
            Id                    = conn.Id,
            Name                  = conn.Name,
            Protocol              = conn.Protocol,
            Hostname              = conn.Hostname,
            Port                  = conn.Port,
            Username              = conn.Username,
            IgnoreCert            = conn.IgnoreCert,
            Security              = conn.Security,
            Notes                 = conn.Notes,
            IsActive              = conn.IsActive,
            CustomerId            = conn.CustomerId,
            GuacamoleConnectionId = conn.GuacamoleConnectionId,
            CustomerOptions       = await BuildCustomerOptionsAsync()
            // PlainPassword jätetään tarkoituksella tyhjäksi — admin täyttää vain jos haluaa vaihtaa.
            // Olemassaolevaa salattua salasanaa EI palauteta lomakkeelle (turvallisuus).
        };
        return View(model);
    }

    // ── POST /RemoteDesktop/Edit/5 ───────────────────────────────────────────
    // Tallentaa muokatun yhteyden.
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

        // Päivitetään kentät yksi kerrallaan.
        conn.Name                  = model.Name;
        conn.Protocol              = model.Protocol;
        conn.Hostname              = model.Hostname;
        conn.Port                  = model.Port;
        conn.Username              = model.Username;
        conn.IgnoreCert            = model.IgnoreCert;
        conn.Security              = model.Security;
        conn.Notes                 = model.Notes;
        conn.IsActive              = model.IsActive;
        conn.CustomerId            = model.CustomerId;
        conn.GuacamoleConnectionId = model.GuacamoleConnectionId;

        // Salasana päivitetään vain jos admin on syöttänyt uuden salasanan.
        // Jos lomake lähetetään tyhjällä PlainPassword-kentällä, vanha salasana säilyy.
        if (!string.IsNullOrEmpty(model.PlainPassword))
            conn.EncryptedPassword = _guacamole.ProtectPassword(model.PlainPassword);

        await _db.SaveChangesAsync(); // UPDATE SQL.

        _logger.LogInformation(
            "Etätyöpöytäyhteys '{Name}' (Id={Id}) päivitetty.", conn.Name, id);
        TempData["SuccessMessage"] = $"Yhteys \"{conn.Name}\" päivitetty.";
        return RedirectToAction(nameof(Manage));
    }

    // ── POST /RemoteDesktop/Delete/5 ────────────────────────────────────────
    // "Poistaa" yhteyden — todellisuudessa pehmeä poisto (IsActive=false).
    // Pehmeä poisto: yhteys säilyy tietokannassa historiaa varten, mutta
    // asiakkaat eivät enää näe sitä.
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var conn = await _db.RemoteDesktopConnections.FindAsync(id);
        if (conn is null) return NotFound();

        // Pehmeä poisto: asetetaan IsActive=false → yhteys piiloutuu asiakkaalta.
        // Ei poisteta tietokantariviltä — säilyy historiaa ja mahdollista palauttamista varten.
        conn.IsActive = false;
        await _db.SaveChangesAsync(); // UPDATE SQL.

        _logger.LogInformation(
            "Etätyöpöytäyhteys '{Name}' (Id={Id}) poistettu käytöstä.", conn.Name, id);
        TempData["SuccessMessage"] = $"Yhteys \"{conn.Name}\" poistettu käytöstä.";
        return RedirectToAction(nameof(Manage));
    }

    // ── GET /RemoteDesktop/Connect/5 ────────────────────────────────────────
    // Avaa etätyöpöytäyhteyden selaimessa Apache Guacamolen kautta.
    public async Task<IActionResult> Connect(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        // Haetaan yhteys asiakkaan tiedoilla — tarvitaan IDOR-tarkistukseen.
        var conn = await _db.RemoteDesktopConnections
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive); // Vain aktiiviset.

        if (conn is null) return NotFound();

        // IDOR-suoja: asiakas saa avata vain oman yrityksensä yhteydet.
        // Ilman tätä tarkistusta asiakas voisi muuttaa URL:n id:tä ja avata
        // toisen yrityksen palvelimeen yhteyden.
        if (!isAdmin && conn.CustomerId != currentUser?.CustomerId)
            return Forbid(); // HTTP 403 — sinulla ei ole oikeutta tähän yhteyteen.

        // Rakennetaan Guacamole-yhteyslinkki palvelun kautta.
        // Tämä generoi allekirjoitetun tokenin tai session Guacamole-palvelimelle.
        string? url = await _guacamole.BuildConnectionUrlAsync(conn);

        // Kirjataan yhteyden avaus lokiin turvallisuusseurantaa varten.
        // Kuka avasi minkä yhteyden? — tärkeää tietoturva-auditoinnissa.
        _logger.LogInformation(
            "Käyttäjä {Email} avaa etäyhteyden '{Name}' (Id={Id}).",
            currentUser?.Email ?? "tuntematon", conn.Name, id);

        // Jos Guacamole-URL on tyhjä, yhteys on virheellisesti konfiguroitu.
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning(
                "Guacamole-URL on tyhjä yhteydelle '{Name}' (Id={Id}).", conn.Name, id);
            TempData["ErrorMessage"] =
                "Etäyhteyden muodostaminen epäonnistui. Tarkista yhteyden asetukset.";
            return RedirectToAction(nameof(Index));
        }

        // Välitetään yhteysnimi ja Guacamole-URL näkymälle.
        // Näkymä upottaa Guacamolen iframeen tai ohjaa suoraan Guacamole-URL:iin.
        ViewBag.ConnectionName = conn.Name;
        ViewBag.GuacamoleUrl   = url;
        return View();
    }

    // ── Yksityinen apumetodi ─────────────────────────────────────────────────
    // Rakentaa pudotusvalikon aktiivisista asiakkaista admin-lomakkeita varten.
    private async Task<IEnumerable<SelectListItem>> BuildCustomerOptionsAsync() =>
        (await _db.Customers
            .Where(c => c.IsActive)
            .OrderBy(c => c.CompanyName)
            .ToListAsync())
        // SelectListItem(näytettävä teksti, lomakkeelle lähetettävä arvo).
        .Select(c => new SelectListItem(c.CompanyName, c.Id.ToString()));
}
