// Nimiavaruuksien tuonnit.
using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Tiedotteiden hallinta. Vain admin-rooli pääsee — asiakaskäyttäjä
/// näkee tiedotteet etusivulla mutta ei voi muokata.
/// </summary>
// Koko controller on rajoitettu Admin-roolille — yksittäisillä action-metodeilla
// ei tarvita enää omia [Authorize]-attribuutteja.
[Authorize(Roles = "Admin")]
public class AnnouncementController : Controller
{
    // Tietokantayhteys — injektoitu konstruktorissa.
    private readonly ApplicationDbContext         _db;
    // UserManager tarvitaan kun haetaan kirjautunut admin tallennusta varten.
    private readonly UserManager<ApplicationUser> _userManager;

    // Konstruktori: DI-säiliö täyttää parametrit.
    public AnnouncementController(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager)
    {
        _db          = db;
        _userManager = userManager;
    }

    // ── GET /Announcement ─────────────────────────────────────────────────────
    // Listaa kaikki tiedotteet uusimmasta vanhimpaan.
    public async Task<IActionResult> Index()
    {
        // Include(a => a.CreatedByUser) = JOIN: haetaan myös luojan tiedot.
        // Tarvitaan koska listanäkymässä näytetään kuka tiedotteen loi.
        var items = await _db.Announcements
            .Include(a => a.CreatedByUser)
            .OrderByDescending(a => a.CreatedAt)  // Uusin ensin.
            .ToListAsync();
        return View(items);
    }

    // ── GET /Announcement/Create ──────────────────────────────────────────────
    // Tyhjä luontilomake — ei tarvita tietokantakutsuja.
    public IActionResult Create() => View(new AnnouncementFormViewModel());

    // ── POST /Announcement/Create ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AnnouncementFormViewModel model)
    {
        // Validoidaan lomake.
        if (!ModelState.IsValid) return View(model);

        // Haetaan kirjautunut admin — tallennetaan CreatedByUserId-kenttään.
        var currentUser = await _userManager.GetUserAsync(User);

        // Luodaan entiteetti ViewModelista.
        var ann = new Announcement
        {
            Title           = model.Title,
            Content         = model.Content,
            Type            = model.Type,
            // ToUniversalTime() muuntaa paikallisen ajan UTC:ksi tallennusta varten.
            // Lomakkeella käyttäjä syöttää paikallista aikaa, mutta kantaan tallennetaan UTC.
            ValidFrom       = model.ValidFrom?.ToUniversalTime(),
            ValidUntil      = model.ValidUntil?.ToUniversalTime(),
            IsPublished     = model.IsPublished,
            // "!" = null-forgiving operator: varmistamme että currentUser ei ole null
            // (admin on aina kirjautunut koska [Authorize(Roles="Admin")] on päällä).
            CreatedByUserId = currentUser!.Id,
            CreatedAt       = DateTime.UtcNow
        };
        // Lisätään tietokantaan.
        _db.Announcements.Add(ann);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Tiedote luotu.";
        return RedirectToAction(nameof(Index));
    }

    // ── GET /Announcement/Edit/5 ──────────────────────────────────────────────
    // Muokkauslomake — täytetään olemassaolevalla tiedotteella.
    public async Task<IActionResult> Edit(int id)
    {
        // Haetaan muokattava tiedote ID:llä.
        var ann = await _db.Announcements.FindAsync(id);
        if (ann is null) return NotFound();

        // Muunnetaan entiteetti ViewModeliksi muokkausta varten.
        return View(new AnnouncementFormViewModel
        {
            Title       = ann.Title,
            Content     = ann.Content,
            Type        = ann.Type,
            // ToLocalTime() muuntaa kantaan tallennetun UTC:n takaisin paikalliseksi ajaksi.
            // Käyttäjä näkee lomakkeella paikallisen ajan.
            ValidFrom   = ann.ValidFrom?.ToLocalTime(),
            ValidUntil  = ann.ValidUntil?.ToLocalTime(),
            IsPublished = ann.IsPublished
        });
    }

    // ── POST /Announcement/Edit/5 ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AnnouncementFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Haetaan alkuperäinen entiteetti.
        var ann = await _db.Announcements.FindAsync(id);
        if (ann is null) return NotFound();

        // Päivitetään kentät.
        ann.Title       = model.Title;
        ann.Content     = model.Content;
        ann.Type        = model.Type;
        ann.ValidFrom   = model.ValidFrom?.ToUniversalTime();   // Paikallinen → UTC.
        ann.ValidUntil  = model.ValidUntil?.ToUniversalTime();  // Paikallinen → UTC.
        ann.IsPublished = model.IsPublished;

        // EF Core tunnistaa muutokset automaattisesti (change tracking) → UPDATE SQL.
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "Tiedote päivitetty.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Announcement/Delete/5 ───────────────────────────────────────────
    // Poistaa tiedotteen pysyvästi tietokannasta.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ann = await _db.Announcements.FindAsync(id);
        if (ann is null) return NotFound();

        // Remove() + SaveChangesAsync() = DELETE SQL.
        _db.Announcements.Remove(ann);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "Tiedote poistettu.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Announcement/TogglePublish/5 ────────────────────────────────────
    // Vaihtaa julkaisutilan (julkinen ↔ luonnos).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var ann = await _db.Announcements.FindAsync(id);
        if (ann is null) return NotFound();

        // Käännetään julkaisutila päinvastaiseksi.
        ann.IsPublished = !ann.IsPublished;
        await _db.SaveChangesAsync();
        // Viesti vaihtelee tilanteen mukaan.
        TempData["SuccessMessage"] = ann.IsPublished ? "Tiedote julkaistu." : "Tiedote piilotettu.";
        return RedirectToAction(nameof(Index));
    }
}
