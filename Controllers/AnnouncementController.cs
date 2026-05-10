using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

/// <summary>
/// Tiedotteiden hallinta. Vain admin-rooli pääsee — asiakaskäyttäjä
/// näkee tiedotteet etusivulla mutta ei voi muokata.
/// </summary>
[Authorize(Roles = "Admin")]
public class AnnouncementController : Controller
{
    private readonly ApplicationDbContext         _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AnnouncementController(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager)
    {
        _db          = db;
        _userManager = userManager;
    }

    // ── GET /Announcement ─────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var items = await _db.Announcements
            .Include(a => a.CreatedByUser)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return View(items);
    }

    // ── GET /Announcement/Create ──────────────────────────────────────
    public IActionResult Create() => View(new AnnouncementFormViewModel());

    // ── POST /Announcement/Create ─────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AnnouncementFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var currentUser = await _userManager.GetUserAsync(User);
        var ann = new Announcement
        {
            Title           = model.Title,
            Content         = model.Content,
            Type            = model.Type,
            ValidFrom       = model.ValidFrom?.ToUniversalTime(),
            ValidUntil      = model.ValidUntil?.ToUniversalTime(),
            IsPublished     = model.IsPublished,
            CreatedByUserId = currentUser!.Id,
            CreatedAt       = DateTime.UtcNow
        };
        _db.Announcements.Add(ann);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Tiedote luotu.";
        return RedirectToAction(nameof(Index));
    }

    // ── GET /Announcement/Edit/5 ──────────────────────────────────────
    public async Task<IActionResult> Edit(int id)
    {
        var ann = await _db.Announcements.FindAsync(id);
        if (ann is null) return NotFound();

        return View(new AnnouncementFormViewModel
        {
            Title       = ann.Title,
            Content     = ann.Content,
            Type        = ann.Type,
            ValidFrom   = ann.ValidFrom?.ToLocalTime(),
            ValidUntil  = ann.ValidUntil?.ToLocalTime(),
            IsPublished = ann.IsPublished
        });
    }

    // ── POST /Announcement/Edit/5 ─────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AnnouncementFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var ann = await _db.Announcements.FindAsync(id);
        if (ann is null) return NotFound();

        ann.Title       = model.Title;
        ann.Content     = model.Content;
        ann.Type        = model.Type;
        ann.ValidFrom   = model.ValidFrom?.ToUniversalTime();
        ann.ValidUntil  = model.ValidUntil?.ToUniversalTime();
        ann.IsPublished = model.IsPublished;

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "Tiedote päivitetty.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Announcement/Delete/5 ───────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ann = await _db.Announcements.FindAsync(id);
        if (ann is null) return NotFound();

        _db.Announcements.Remove(ann);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "Tiedote poistettu.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Announcement/TogglePublish/5 ────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var ann = await _db.Announcements.FindAsync(id);
        if (ann is null) return NotFound();

        ann.IsPublished = !ann.IsPublished;
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = ann.IsPublished ? "Tiedote julkaistu." : "Tiedote piilotettu.";
        return RedirectToAction(nameof(Index));
    }
}
