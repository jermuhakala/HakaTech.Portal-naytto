// Nimiavaruuksien tuonnit.
using HakaTech.Portal.Data;                        // ApplicationDbContext — tietokantayhteys.
using HakaTech.Portal.Models.Domain;               // ServiceCatalogItem, QuoteRequest, QuoteRequestStatus...
using HakaTech.Portal.Models.ViewModels;           // ServiceCatalogItemFormViewModel, QuoteRequestFormViewModel...
using Microsoft.AspNetCore.Authorization;          // [Authorize]-attribuutti.
using Microsoft.AspNetCore.Identity;               // UserManager.
using Microsoft.AspNetCore.Mvc;                    // Controller, IActionResult, TempData...
using Microsoft.EntityFrameworkCore;               // Include, ToListAsync, FindAsync...

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Palvelukatalogin controller. Asiakas selaa palveluita ja voi pyytää
/// tarjouksen (QuoteRequest). Admin hallinnoi palveluita ja käsittelee
/// tarjouspyyntöjä.
/// </summary>
// [Authorize] = kirjautuminen vaaditaan kaikkiin toimintoihin.
[Authorize]
public class ServiceCatalogController : Controller
{
    // Tietokantayhteys.
    private readonly ApplicationDbContext             _db;
    // UserManager — haetaan kirjautunut käyttäjä ja tarkistetaan rooli.
    private readonly UserManager<ApplicationUser>     _userManager;
    // Diagnostiikkaloki kehittäjälle.
    private readonly ILogger<ServiceCatalogController> _logger;

    // Konstruktori: DI-säiliö täyttää parametrit.
    public ServiceCatalogController(
        ApplicationDbContext              db,
        UserManager<ApplicationUser>      userManager,
        ILogger<ServiceCatalogController> logger)
    {
        _db          = db;
        _userManager = userManager;
        _logger      = logger;
    }

    // ── GET /ServiceCatalog ──────────────────────────────────────────────────
    // Asiakkaan näkymä: selaa aktiivisia palveluita ja pyydä tarjous.
    public async Task<IActionResult> Index()
    {
        // Haetaan vain aktiiviset (IsActive=true) palvelut.
        // Ei-aktiiviset ovat piilotettu — admin voi silti nähdä ne Manage-sivulla.
        var services = await _db.ServiceCatalogItems
            .Where(s => s.IsActive)
            // Järjestetään ensin kategorian mukaan, sitten nimen mukaan — ryhmitelty lista.
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Name)
            .ToListAsync();
        return View(services);
    }

    // ── GET /ServiceCatalog/Manage (Admin) ───────────────────────────────────
    // Adminin näkymä: kaikki palvelut myös piilotetut.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Manage()
    {
        var services = await _db.ServiceCatalogItems
            // OrderByDescending(IsActive) = aktiiviset ensin (true > false).
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.Name) // Sama tila → aakkosjärjestys.
            .ToListAsync();
        return View(services);
    }

    // ── GET /ServiceCatalog/Create ───────────────────────────────────────────
    // Tyhjä palvelun luontilomake.
    [Authorize(Roles = "Admin")]
    // Yksirivinen metodi: => View(...) on lyhenne yksinkertaiselle palautukselle.
    public IActionResult Create() => View(new ServiceCatalogItemFormViewModel());

    // ── POST /ServiceCatalog/Create ──────────────────────────────────────────
    // Tallentaa uuden palvelun tietokantaan.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceCatalogItemFormViewModel model)
    {
        // Validoidaan lomake (nimi pakollinen, hinta vapaaehtoinen).
        if (!ModelState.IsValid) return View(model);

        // Luodaan uusi palveluentiteetti ViewModelista.
        var item = new ServiceCatalogItem
        {
            Name        = model.Name,
            Description = model.Description,
            Category    = model.Category,
            // null = "pyydä tarjous" — hintaa ei näytetä vaan kehotetaan ottamaan yhteyttä.
            Price       = model.Price,
            IsActive    = model.IsActive, // false = piilotettu asiakkailta (luonnos).
            CreatedAt   = DateTime.UtcNow
        };
        _db.ServiceCatalogItems.Add(item);
        await _db.SaveChangesAsync(); // INSERT SQL.

        _logger.LogInformation("Palvelukatalogipalvelu '{Name}' luotu.", item.Name);
        TempData["SuccessMessage"] = $"Palvelu \"{item.Name}\" luotu.";
        return RedirectToAction(nameof(Manage));
    }

    // ── GET /ServiceCatalog/Edit/5 ───────────────────────────────────────────
    // Muokkauslomake olemassaolevalle palvelulle.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.ServiceCatalogItems.FindAsync(id);
        if (item is null) return NotFound();

        // Täytetään ViewModel olemassaolevasta palvelusta.
        return View(new ServiceCatalogItemFormViewModel
        {
            // Huom: Id puuttuu — muokattava palvelu tunnistetaan URL:n id-parametrista.
            Name        = item.Name,
            Description = item.Description,
            Category    = item.Category,
            Price       = item.Price,
            IsActive    = item.IsActive
        });
    }

    // ── POST /ServiceCatalog/Edit/5 ──────────────────────────────────────────
    // Tallentaa muokatun palvelun.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ServiceCatalogItemFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var item = await _db.ServiceCatalogItems.FindAsync(id);
        if (item is null) return NotFound();

        // Päivitetään kentät.
        item.Name        = model.Name;
        item.Description = model.Description;
        item.Category    = model.Category;
        item.Price       = model.Price;    // null sallittu — "pyydä tarjous".
        item.IsActive    = model.IsActive;

        await _db.SaveChangesAsync(); // UPDATE SQL.
        TempData["SuccessMessage"] = "Palvelu päivitetty.";
        return RedirectToAction(nameof(Manage));
    }

    // ── POST /ServiceCatalog/Delete/5 ────────────────────────────────────────
    // "Poistaa" palvelun — todellisuudessa pehmeä poisto (IsActive=false).
    // Pehmeä poisto: palvelu piiloutuu asiakkailta mutta säilyy tietokannassa.
    // Aiemmin luodut tarjouspyynnöt viittaavat edelleen palveluun (viitteellinen eheys).
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.ServiceCatalogItems.FindAsync(id);
        if (item is null) return NotFound();

        // Ei oikeaa poistamista — asetetaan IsActive=false.
        item.IsActive = false;
        await _db.SaveChangesAsync(); // UPDATE SQL.
        TempData["SuccessMessage"] = $"Palvelu \"{item.Name}\" poistettu käytöstä.";
        return RedirectToAction(nameof(Manage));
    }

    // ── POST /ServiceCatalog/RequestQuote ────────────────────────────────────
    // Asiakas pyytää tarjousta palvelusta — luo QuoteRequest-tietueen.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestQuote(QuoteRequestFormViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);

        // Vain asiakaskäyttäjät voivat pyytää tarjouksia.
        // Admin ei pyydä tarjouksia — hän käsittelee niitä.
        // currentUser?.CustomerId is null = käyttäjällä ei ole yritystä.
        if (User.IsInRole("Admin") || currentUser?.CustomerId is null)
        {
            TempData["ErrorMessage"] = "Vain asiakaskäyttäjät voivat pyytää tarjouksia.";
            return RedirectToAction(nameof(Index));
        }

        // Tarkistetaan että palvelu on olemassa ja aktiivinen.
        var service = await _db.ServiceCatalogItems.FindAsync(model.ServiceCatalogItemId);
        // !service.IsActive = palvelu on piilotettu — ei voi pyytää tarjousta piilotetulle.
        if (service is null || !service.IsActive) return NotFound();

        // Luodaan tarjouspyyntöentiteetti.
        var request = new QuoteRequest
        {
            ServiceCatalogItemId = model.ServiceCatalogItemId,
            // .Value — CustomerId on nullable int?, tiedetään se ei ole null (tarkistettiin yllä).
            CustomerId           = currentUser.CustomerId.Value,
            CreatedByUserId      = currentUser.Id,
            Message              = model.Message,          // Asiakkaan viesti adminille.
            Status               = QuoteRequestStatus.Pending, // Alkutila: odottaa adminin vastausta.
            CreatedAt            = DateTime.UtcNow,
            UpdatedAt            = DateTime.UtcNow
        };
        _db.QuoteRequests.Add(request);
        await _db.SaveChangesAsync(); // INSERT SQL.

        _logger.LogInformation(
            "Tarjouspyyntö #{Id} palvelulle '{Service}' luotu.", request.Id, service.Name);
        TempData["SuccessMessage"] =
            $"Tarjouspyyntö palvelulle \"{service.Name}\" lähetetty. Olemme yhteydessä pian!";
        // Ohjataan takaisin katalogiin — ei tarjouspyyntöjen listaan (se on vain adminille).
        return RedirectToAction(nameof(Index));
    }

    // ── GET /ServiceCatalog/Requests (Admin) ────────────────────────────────
    // Admin näkee kaikki tarjouspyynnöt tilasuodattimella.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Requests(
        QuoteRequestStatus? status) // Tilasuodatin. null = kaikki tilat.
    {
        // Aloitetaan kyselyobjektilla jolle lisätään suodatin alla.
        var query = _db.QuoteRequests
            .Include(q => q.Service)       // Palvelun nimi.
            .Include(q => q.Customer)      // Asiakkaan yritysnimi.
            .Include(q => q.CreatedByUser) // Tarjouspyynnön luoneen käyttäjän tiedot.
            .AsQueryable();

        // Tilasuodatin — lisätään vain jos parametri on annettu.
        if (status.HasValue)
            query = query.Where(q => q.Status == status.Value);

        // Uusimmasta vanhimpaan.
        var requests = await query
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        // Välitetään suodatintila näkymälle (lomake pysyy täytettynä).
        ViewBag.StatusFilter = status;
        return View(requests);
    }

    // ── GET /ServiceCatalog/RequestDetails/5 (Admin) ────────────────────────
    // Yksittäisen tarjouspyynnön tiedot — admin näkee viestin ja voi päivittää tilan.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RequestDetails(int id)
    {
        // Haetaan tarjouspyyntö kaikilla suhteilla.
        var request = await _db.QuoteRequests
            .Include(q => q.Service)       // Palvelun nimi ja tiedot.
            .Include(q => q.Customer)      // Asiakasyrityksen tiedot.
            .Include(q => q.CreatedByUser) // Pyytäjän nimi ja sähköposti.
            .FirstOrDefaultAsync(q => q.Id == id);

        if (request is null) return NotFound();

        // Esitäytetty päivityslomake — admin muuttaa tilaa ja lisää sisäisiä muistiinpanoja.
        ViewBag.UpdateModel = new QuoteRequestUpdateViewModel
        {
            Id         = request.Id,
            Status     = request.Status,
            AdminNotes = request.AdminNotes // Sisäiset muistiinpanot (asiakas ei näe).
        };
        return View(request);
    }

    // ── POST /ServiceCatalog/UpdateRequest (Admin) ───────────────────────────
    // Admin päivittää tarjouspyynnön tilan ja sisäiset muistiinpanot.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRequest(QuoteRequestUpdateViewModel model)
    {
        var request = await _db.QuoteRequests.FindAsync(model.Id);
        if (request is null) return NotFound();

        // Päivitetään tarjouspyynnön tila ja muistiinpanot.
        request.Status     = model.Status;     // Esim. Pending → Quoted tai Accepted.
        request.AdminNotes = model.AdminNotes; // Sisäiset muistiinpanot — vain admin näkee.
        request.UpdatedAt  = DateTime.UtcNow;  // Päivitetään muokkausaika.
        await _db.SaveChangesAsync(); // UPDATE SQL.

        TempData["SuccessMessage"] = "Tarjouspyyntö päivitetty.";
        // Ohjataan takaisin saman tarjouspyynnön tietosivulle.
        return RedirectToAction(nameof(RequestDetails), new { id = model.Id });
    }
}
