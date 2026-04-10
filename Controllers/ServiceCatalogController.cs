using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

[Authorize]
public class ServiceCatalogController : Controller
{
    private readonly ApplicationDbContext          _db;
    private readonly UserManager<ApplicationUser>  _userManager;
    private readonly ILogger<ServiceCatalogController> _logger;

    public ServiceCatalogController(
        ApplicationDbContext          db,
        UserManager<ApplicationUser>  userManager,
        ILogger<ServiceCatalogController> logger)
    {
        _db          = db;
        _userManager = userManager;
        _logger      = logger;
    }

    // ── GET /ServiceCatalog (Asiakkaalle: selaa palveluja) ────────────
    public async Task<IActionResult> Index()
    {
        var services = await _db.ServiceCatalogItems
            .Where(s => s.IsActive)
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Name)
            .ToListAsync();
        return View(services);
    }

    // ── GET /ServiceCatalog/Manage (Admin) ────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Manage()
    {
        var services = await _db.ServiceCatalogItems
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.Name)
            .ToListAsync();
        return View(services);
    }

    // ── GET /ServiceCatalog/Create ────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public IActionResult Create() => View(new ServiceCatalogItemFormViewModel());

    // ── POST /ServiceCatalog/Create ───────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceCatalogItemFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var item = new ServiceCatalogItem
        {
            Name        = model.Name,
            Description = model.Description,
            Category    = model.Category,
            Price       = model.Price,
            IsActive    = model.IsActive,
            CreatedAt   = DateTime.UtcNow
        };
        _db.ServiceCatalogItems.Add(item);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Palvelukatalogipalvelu '{Name}' luotu.", item.Name);
        TempData["SuccessMessage"] = $"Palvelu \"{item.Name}\" luotu.";
        return RedirectToAction(nameof(Manage));
    }

    // ── GET /ServiceCatalog/Edit/5 ────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.ServiceCatalogItems.FindAsync(id);
        if (item is null) return NotFound();

        return View(new ServiceCatalogItemFormViewModel
        {
            Name        = item.Name,
            Description = item.Description,
            Category    = item.Category,
            Price       = item.Price,
            IsActive    = item.IsActive
        });
    }

    // ── POST /ServiceCatalog/Edit/5 ───────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ServiceCatalogItemFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var item = await _db.ServiceCatalogItems.FindAsync(id);
        if (item is null) return NotFound();

        item.Name        = model.Name;
        item.Description = model.Description;
        item.Category    = model.Category;
        item.Price       = model.Price;
        item.IsActive    = model.IsActive;

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "Palvelu päivitetty.";
        return RedirectToAction(nameof(Manage));
    }

    // ── POST /ServiceCatalog/Delete/5 ─────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.ServiceCatalogItems.FindAsync(id);
        if (item is null) return NotFound();

        item.IsActive = false;
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Palvelu \"{item.Name}\" poistettu käytöstä.";
        return RedirectToAction(nameof(Manage));
    }

    // ── POST /ServiceCatalog/RequestQuote ─────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestQuote(QuoteRequestFormViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);

        if (User.IsInRole("Admin") || currentUser?.CustomerId is null)
        {
            TempData["ErrorMessage"] = "Vain asiakaskäyttäjät voivat pyytää tarjouksia.";
            return RedirectToAction(nameof(Index));
        }

        var service = await _db.ServiceCatalogItems.FindAsync(model.ServiceCatalogItemId);
        if (service is null || !service.IsActive) return NotFound();

        var request = new QuoteRequest
        {
            ServiceCatalogItemId = model.ServiceCatalogItemId,
            CustomerId           = currentUser.CustomerId.Value,
            CreatedByUserId      = currentUser.Id,
            Message              = model.Message,
            Status               = QuoteRequestStatus.Pending,
            CreatedAt            = DateTime.UtcNow,
            UpdatedAt            = DateTime.UtcNow
        };
        _db.QuoteRequests.Add(request);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Tarjouspyyntö #{Id} palvelulle '{Service}' luotu.", request.Id, service.Name);
        TempData["SuccessMessage"] = $"Tarjouspyyntö palvelulle \"{service.Name}\" lähetetty. Olemme yhteydessä pian!";
        return RedirectToAction(nameof(Index));
    }

    // ── GET /ServiceCatalog/Requests (Admin) ──────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Requests(QuoteRequestStatus? status)
    {
        var query = _db.QuoteRequests
            .Include(q => q.Service)
            .Include(q => q.Customer)
            .Include(q => q.CreatedByUser)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(q => q.Status == status.Value);

        var requests = await query
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        ViewBag.StatusFilter = status;
        return View(requests);
    }

    // ── GET /ServiceCatalog/RequestDetails/5 (Admin) ──────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RequestDetails(int id)
    {
        var request = await _db.QuoteRequests
            .Include(q => q.Service)
            .Include(q => q.Customer)
            .Include(q => q.CreatedByUser)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (request is null) return NotFound();

        ViewBag.UpdateModel = new QuoteRequestUpdateViewModel
        {
            Id         = request.Id,
            Status     = request.Status,
            AdminNotes = request.AdminNotes
        };
        return View(request);
    }

    // ── POST /ServiceCatalog/UpdateRequest (Admin) ────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRequest(QuoteRequestUpdateViewModel model)
    {
        var request = await _db.QuoteRequests.FindAsync(model.Id);
        if (request is null) return NotFound();

        request.Status     = model.Status;
        request.AdminNotes = model.AdminNotes;
        request.UpdatedAt  = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Tarjouspyyntö päivitetty.";
        return RedirectToAction(nameof(RequestDetails), new { id = model.Id });
    }
}
