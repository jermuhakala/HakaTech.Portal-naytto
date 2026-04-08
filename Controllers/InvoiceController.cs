using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

[Authorize]
public class InvoiceController : Controller
{
    private readonly ApplicationDbContext         _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<InvoiceController>   _logger;

    public InvoiceController(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager,
        ILogger<InvoiceController>   logger)
    {
        _db          = db;
        _userManager = userManager;
        _logger      = logger;
    }

    // ── GET /Invoice ─────────────────────────────────────────────────
    public async Task<IActionResult> Index(int? customerId, InvoiceStatus? status)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var query = _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .AsQueryable();

        // Asiakaskäyttäjä näkee vain oman yrityksensä laskut
        if (!isAdmin)
        {
            if (currentUser?.CustomerId is null)
                return View(Enumerable.Empty<Invoice>());

            query = query.Where(i => i.CustomerId == currentUser.CustomerId);
        }
        else if (customerId.HasValue)
        {
            query = query.Where(i => i.CustomerId == customerId.Value);
        }

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        var invoices = await query
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();

        ViewBag.StatusFilter   = status;
        ViewBag.CustomerFilter = customerId;
        ViewBag.IsAdmin        = isAdmin;
        return View(invoices);
    }

    // ── GET /Invoice/Details/5 ───────────────────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var invoice = await _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice is null)
            return NotFound();

        if (!isAdmin && invoice.CustomerId != currentUser?.CustomerId)
            return Forbid();

        ViewBag.IsAdmin      = isAdmin;

        var history = await _db.Invoices
            .Where(i => i.CustomerId == invoice.CustomerId && i.Id != invoice.Id)
            .OrderByDescending(i => i.InvoiceDate)
            .Take(5)
            .ToListAsync();
        ViewBag.CustomerHistory = history;

        ViewBag.StatusModel  = new InvoiceStatusViewModel
        {
            Id     = invoice.Id,
            Status = invoice.Status,
            PaidAt = invoice.PaidAt
        };

        return View(invoice);
    }

    // ── GET /Invoice/DownloadPdf/5 ──────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> DownloadPdf(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var invoice = await _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice is null) return NotFound();

        if (!isAdmin && invoice.CustomerId != currentUser?.CustomerId)
            return Forbid();

        var document = new HakaTech.Portal.Services.InvoicePdfDocument(invoice);
        var pdfBytes = QuestPDF.Fluent.GenerateExtensions.GeneratePdf(document);

        return File(pdfBytes, "application/pdf", $"Lasku_{invoice.InvoiceNumber}.pdf");
    }

    // ── GET /Invoice/Create ──────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(int? customerId)
    {
        var model = new InvoiceCreateViewModel
        {
            CustomerId      = customerId ?? 0,
            CustomerOptions = await BuildCustomerOptions(),
            // Ehdota seuraavaa laskunumeroa  INV-YYYY-NNN
            InvoiceNumber   = await SuggestInvoiceNumber()
        };
        return View(model);
    }

    // ── POST /Invoice/Create ─────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceCreateViewModel model)
    {
        // Vähintään yksi rivi
        if (model.Lines is null || model.Lines.Count == 0 ||
            model.Lines.All(l => string.IsNullOrWhiteSpace(l.Description)))
        {
            ModelState.AddModelError(string.Empty,
                "Laskulle on lisättävä vähintään yksi rivi.");
        }

        // Laskunumeron uniikkius
        if (await _db.Invoices.AnyAsync(i => i.InvoiceNumber == model.InvoiceNumber))
        {
            ModelState.AddModelError(nameof(model.InvoiceNumber),
                "Laskunumero on jo käytössä.");
        }

        if (!ModelState.IsValid)
        {
            model.CustomerOptions = await BuildCustomerOptions();
            return View(model);
        }

        var invoice = new Invoice
        {
            InvoiceNumber = model.InvoiceNumber,
            CustomerId    = model.CustomerId,
            InvoiceDate   = model.InvoiceDate,
            DueDate       = model.DueDate,
            VatRate       = model.VatRate,
            Notes         = model.Notes,
            Status        = InvoiceStatus.Draft,
            Lines         = model.Lines!
                .Where(l => !string.IsNullOrWhiteSpace(l.Description))
                .Select(l => new InvoiceLine
                {
                    Description = l.Description,
                    Quantity    = l.Quantity,
                    UnitPrice   = l.UnitPrice
                }).ToList()
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Lasku {Num} luotu (Id={Id}).", invoice.InvoiceNumber, invoice.Id);
        TempData["SuccessMessage"] = $"Lasku {invoice.InvoiceNumber} luotu.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    // ── POST /Invoice/UpdateStatus ───────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(InvoiceStatusViewModel model)
    {
        var invoice = await _db.Invoices.FindAsync(model.Id);
        if (invoice is null) return NotFound();

        invoice.Status = model.Status;

        if (model.Status == InvoiceStatus.Paid)
            invoice.PaidAt = model.PaidAt ?? DateTime.UtcNow;
        else if (model.Status != InvoiceStatus.Paid)
            invoice.PaidAt = null;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Laskun {Num} tila → {Status}.", invoice.InvoiceNumber, invoice.Status);
        TempData["SuccessMessage"] = "Laskun tila päivitetty.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    // ── Apumetodit ───────────────────────────────────────────────────
    private async Task<IEnumerable<SelectListItem>> BuildCustomerOptions() =>
        (await _db.Customers
            .Where(c => c.IsActive)
            .OrderBy(c => c.CompanyName)
            .ToListAsync())
        .Select(c => new SelectListItem(c.CompanyName, c.Id.ToString()));

    private async Task<string> SuggestInvoiceNumber()
    {
        int year  = DateTime.Today.Year;
        int count = await _db.Invoices
            .CountAsync(i => i.InvoiceDate.Year == year);
        return $"INV-{year}-{(count + 1):D3}";
    }
}
