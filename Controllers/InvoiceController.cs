using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using HakaTech.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

[Authorize]
public class InvoiceController : Controller
{
    private readonly ApplicationDbContext         _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<InvoiceController>   _logger;
    private readonly IFileStorageService           _fileStorage;
    private readonly IWebHostEnvironment           _env;
    private readonly IEmailService                 _emailService;
    private readonly IAuditService                 _audit;

    public InvoiceController(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager,
        ILogger<InvoiceController>   logger,
        IFileStorageService          fileStorage,
        IWebHostEnvironment          env,
        IEmailService                emailService,
        IAuditService                audit)
    {
        _db           = db;
        _userManager  = userManager;
        _logger       = logger;
        _fileStorage  = fileStorage;
        _env          = env;
        _emailService = emailService;
        _audit        = audit;
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
            .Include(i => i.Attachments.OrderBy(a => a.UploadedAt))
                .ThenInclude(a => a.UploadedByUser)
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

        await _audit.LogAsync("InvoiceDownloaded", "Invoice", id.ToString(),
            $"Lasku {invoice.InvoiceNumber} / {invoice.Customer?.CompanyName}");

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

        // ── Lähetä sähköposti-ilmoitus asiakkaalle ─────────────────────
        var customer = await _db.Customers.FindAsync(invoice.CustomerId);
        if (customer is not null && !string.IsNullOrWhiteSpace(customer.ContactEmail))
        {
            try
            {
                var html = $"""
                    <div style="font-family:Inter,Arial,sans-serif;max-width:600px;margin:0 auto;color:#1e293b">
                      <div style="background:#2563eb;padding:24px 32px;border-radius:8px 8px 0 0">
                        <h1 style="color:#fff;margin:0;font-size:22px">HakaTech – Uusi lasku</h1>
                      </div>
                      <div style="background:#f8fafc;padding:24px 32px;border-radius:0 0 8px 8px;border:1px solid #e2e8f0">
                        <p style="margin:0 0 16px">Hei <strong>{customer.CompanyName}</strong>,</p>
                        <p style="margin:0 0 16px">Teille on laadittu uusi lasku HakaTech-portaaliin.</p>
                        <table style="width:100%;border-collapse:collapse;margin-bottom:20px">
                          <tr><td style="padding:8px 0;color:#64748b;width:160px">Laskunumero</td><td style="padding:8px 0;font-weight:600">{invoice.InvoiceNumber}</td></tr>
                          <tr><td style="padding:8px 0;color:#64748b">Laskupäivä</td><td style="padding:8px 0">{invoice.InvoiceDate:dd.MM.yyyy}</td></tr>
                          <tr><td style="padding:8px 0;color:#64748b">Eräpäivä</td><td style="padding:8px 0;font-weight:600;color:#dc2626">{invoice.DueDate:dd.MM.yyyy}</td></tr>
                          <tr style="border-top:2px solid #e2e8f0"><td style="padding:12px 0 8px;color:#64748b">Yhteensä (sis. ALV)</td><td style="padding:12px 0 8px;font-size:18px;font-weight:700">{invoice.TotalAmount:N2} €</td></tr>
                        </table>
                        <p style="margin:0 0 20px">Voitte tarkastella laskua kirjautumalla HakaTech-portaaliin.</p>
                        <p style="margin:0;color:#94a3b8;font-size:13px">Tämä on automaattinen viesti. Älä vastaa tähän sähköpostiin.<br>HakaTech IT-palvelut | asiakastuki@hakatech.fi</p>
                      </div>
                    </div>
                    """;

                await _emailService.SendEmailAsync(
                    customer.ContactEmail,
                    $"Lasku {invoice.InvoiceNumber} – HakaTech",
                    html);

                _logger.LogInformation(
                    "Laskuilmoitus {Num} lähetetty osoitteeseen {Email}.",
                    invoice.InvoiceNumber, customer.ContactEmail);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Laskuilmoituksen lähetys epäonnistui (lasku {Num}).", invoice.InvoiceNumber);
            }
        }

        TempData["SuccessMessage"] = $"Lasku {invoice.InvoiceNumber} luotu" +
            (customer is not null && !string.IsNullOrWhiteSpace(customer.ContactEmail)
                ? $" ja lähetetty osoitteeseen {customer.ContactEmail}."
                : ".");
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

    // ── POST /Invoice/UploadAttachment (Admin) ───────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAttachment(int invoiceId, IFormFile file)
    {
        var currentUser = await _userManager.GetUserAsync(User);

        if (currentUser is null) return Unauthorized();

        var invoice = await _db.Invoices.FindAsync(invoiceId);
        if (invoice is null) return NotFound();

        if (file is null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Tiedosto on tyhjä tai puuttuu.";
            return RedirectToAction(nameof(Details), new { id = invoiceId });
        }

        if (file.Length > 20 * 1024 * 1024)
        {
            TempData["ErrorMessage"] = "Tiedosto on liian suuri (max 20 MB).";
            return RedirectToAction(nameof(Details), new { id = invoiceId });
        }

        var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".png", ".jpg", ".jpeg", ".gif", ".zip" };
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !allowedExt.Contains(ext))
        {
            TempData["ErrorMessage"] = $"Tiedostotyyppi '{ext}' ei ole sallittu.";
            return RedirectToAction(nameof(Details), new { id = invoiceId });
        }

        var filePath = await _fileStorage.SaveFileAsync(file, "invoices");

        _db.InvoiceAttachments.Add(new InvoiceAttachment
        {
            InvoiceId        = invoiceId,
            FileName         = Path.GetFileName(file.FileName),
            FilePath         = filePath,
            UploadedAt       = DateTime.UtcNow,
            UploadedByUserId = currentUser.Id
        });
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Tiedosto '{Path.GetFileName(file.FileName)}' lisätty.";
        return RedirectToAction(nameof(Details), new { id = invoiceId });
    }

    // ── GET /Invoice/DownloadAttachment/5 ────────────────────────────
    [HttpGet]
    public async Task<IActionResult> DownloadAttachment(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var attachment = await _db.InvoiceAttachments
            .Include(a => a.Invoice)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attachment is null) return NotFound();

        if (!isAdmin && attachment.Invoice?.CustomerId != currentUser?.CustomerId)
            return Forbid();

        var fullPath = _fileStorage.ResolveSafePath(attachment.FilePath);
        if (fullPath is null || !System.IO.File.Exists(fullPath))
            return NotFound();

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(attachment.FileName, out var contentType))
            contentType = "application/octet-stream";

        return PhysicalFile(fullPath, contentType, attachment.FileName);
    }

    // ── POST /Invoice/DeleteAttachment/5 (Admin) ─────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttachment(int id)
    {
        var attachment = await _db.InvoiceAttachments.FindAsync(id);
        if (attachment is null) return NotFound();

        int invoiceId = attachment.InvoiceId;
        _fileStorage.DeleteFile(attachment.FilePath);
        _db.InvoiceAttachments.Remove(attachment);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Liite poistettu.";
        return RedirectToAction(nameof(Details), new { id = invoiceId });
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
