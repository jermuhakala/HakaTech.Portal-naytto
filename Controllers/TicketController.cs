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
public class TicketController : Controller
{
    private readonly ApplicationDbContext          _db;
    private readonly UserManager<ApplicationUser>  _userManager;
    private readonly ILogger<TicketController>     _logger;
    private readonly IEmailService                 _emailService;
    private readonly IFileStorageService           _fileStorage;
    private readonly IWebHostEnvironment           _env;
    private readonly IAuditService                 _audit;

    public TicketController(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager,
        ILogger<TicketController>    logger,
        IEmailService                emailService,
        IFileStorageService          fileStorage,
        IWebHostEnvironment          env,
        IAuditService                audit)
    {
        _db           = db;
        _userManager  = userManager;
        _logger       = logger;
        _emailService = emailService;
        _fileStorage  = fileStorage;
        _env          = env;
        _audit        = audit;
    }

    // ── GET /Ticket ──────────────────────────────────────────────────
    public async Task<IActionResult> Index(
        int?          customerId,
        TicketStatus? status,
        TicketPriority? priority,
        string?       search)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var query = _db.Tickets
            .Include(t => t.Customer)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .AsQueryable();

        if (!isAdmin)
        {
            if (currentUser?.CustomerId is null)
                return View(Enumerable.Empty<Ticket>());

            query = query.Where(t => t.CustomerId == currentUser.CustomerId);
        }
        else if (customerId.HasValue)
        {
            query = query.Where(t => t.CustomerId == customerId.Value);
        }

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (priority.HasValue)
            query = query.Where(t => t.Priority == priority.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query  = query.Where(t =>
                t.Title.Contains(search) ||
                t.Description.Contains(search));
        }

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        ViewBag.StatusFilter   = status;
        ViewBag.PriorityFilter = priority;
        ViewBag.Search         = search;
        ViewBag.CustomerFilter = customerId;
        ViewBag.IsAdmin        = isAdmin;

        return View(tickets);
    }

    // ── GET /Ticket/Details/5 ────────────────────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var ticket = await _db.Tickets
            .Include(t => t.Customer)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Comments.OrderBy(c => c.CreatedAt))
                .ThenInclude(c => c.Author)
            .Include(t => t.Attachments.OrderBy(a => a.UploadedAt))
                .ThenInclude(a => a.UploadedByUser)
            .Include(t => t.Feedback)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket is null)
            return NotFound();

        if (!isAdmin && ticket.CustomerId != currentUser?.CustomerId)
            return Forbid();

        ViewBag.IsAdmin         = isAdmin;
        ViewBag.CurrentUser     = currentUser;
        ViewBag.CommentModel    = new TicketCommentViewModel { TicketId = id };
        ViewBag.EditModel       = isAdmin ? await BuildEditViewModel(ticket) : null;
        ViewBag.ShowFeedback    = !isAdmin
                                  && ticket.Status == TicketStatus.Closed
                                  && ticket.Feedback is null
                                  && ticket.CreatedByUserId == currentUser?.Id;

        return View(ticket);
    }

    // ── GET /Ticket/Create ───────────────────────────────────────────
    public async Task<IActionResult> Create(int? customerId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var model = new TicketCreateViewModel
        {
            Category        = TicketCategory.Other,
            Priority        = TicketPriority.Normal,
            CustomerOptions = []
        };

        if (isAdmin)
        {
            model.CustomerId      = customerId;
            model.CustomerOptions = await BuildCustomerOptions();
        }
        else
        {
            model.CustomerId = currentUser?.CustomerId;
        }

        return View(model);
    }

    // ── POST /Ticket/Create ──────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TicketCreateViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        if (!isAdmin)
            model.CustomerId = currentUser?.CustomerId;

        if (model.CustomerId is null)
            ModelState.AddModelError(nameof(model.CustomerId), "Asiakas on valittava.");

        if (!ModelState.IsValid)
        {
            if (isAdmin) model.CustomerOptions = await BuildCustomerOptions();
            return View(model);
        }

        var ticket = new Ticket
        {
            Title            = model.Title,
            Description      = model.Description,
            Category         = model.Category,
            Priority         = model.Priority,
            Status           = TicketStatus.Open,
            CustomerId       = model.CustomerId!.Value,
            CreatedByUserId  = currentUser!.Id,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Tiketti #{Id} '{Title}' luotu.", ticket.Id, ticket.Title);
        await _audit.LogAsync("TicketCreated", "Ticket", ticket.Id.ToString(), ticket.Title);
        TempData["SuccessMessage"] = $"Tiketti #{ticket.Id} luotu onnistuneesti.";
        return RedirectToAction(nameof(Details), new { id = ticket.Id });
    }

    // ── POST /Ticket/UpdateStatus (Admin) ────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(TicketEditViewModel model)
    {
        var ticket = await _db.Tickets.Include(t => t.CreatedByUser).FirstOrDefaultAsync(t => t.Id == model.Id);
        if (ticket is null) return NotFound();

        var oldStatus = ticket.Status;

        if (!string.IsNullOrEmpty(model.AssignedToUserId))
        {
            var assignee = await _userManager.FindByIdAsync(model.AssignedToUserId);
            if (assignee is null || !await _userManager.IsInRoleAsync(assignee, "Admin"))
            {
                TempData["ErrorMessage"] = "Vastuuhenkilön tulee olla Admin-roolissa.";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
        }

        ticket.Status           = model.Status;
        ticket.Priority         = model.Priority;
        ticket.AssignedToUserId = model.AssignedToUserId;
        ticket.UpdatedAt        = DateTime.UtcNow;

        if (model.Status == TicketStatus.Resolved && ticket.ResolvedAt is null)
            ticket.ResolvedAt = DateTime.UtcNow;
        else if (model.Status != TicketStatus.Resolved && model.Status != TicketStatus.Closed)
            ticket.ResolvedAt = null;

        await _db.SaveChangesAsync();

        if (oldStatus != model.Status && (model.Status == TicketStatus.InProgress || model.Status == TicketStatus.Closed))
            await SendTicketStatusEmailAsync(ticket, model.Status == TicketStatus.InProgress ? "Otettu työn alle" : "Suljettu");

        _logger.LogInformation("Tiketin #{Id} tila muutettu → {Status}.", ticket.Id, ticket.Status);
        await _audit.LogAsync("TicketStatusChanged", "Ticket", ticket.Id.ToString(),
            $"{oldStatus} → {ticket.Status}");
        TempData["SuccessMessage"] = "Tiketin tila päivitetty.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    // ── POST /Ticket/AddComment ──────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(TicketCommentViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var ticket = await _db.Tickets.FindAsync(model.TicketId);
        if (ticket is null) return NotFound();

        if (!isAdmin) model.IsInternal = false;

        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Details), new { id = model.TicketId });

        var comment = new TicketComment
        {
            TicketId   = model.TicketId,
            Content    = model.Content,
            IsInternal = model.IsInternal,
            AuthorId   = currentUser!.Id,
            CreatedAt  = DateTime.UtcNow
        };

        ticket.UpdatedAt = DateTime.UtcNow;

        if (!isAdmin && ticket.Status == TicketStatus.WaitingCustomer)
            ticket.Status = TicketStatus.InProgress;

        _db.TicketComments.Add(comment);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = model.TicketId });
    }

    // ── POST /Ticket/Close ───────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var ticket = await _db.Tickets.Include(t => t.CreatedByUser).FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();

        if (!isAdmin && ticket.CreatedByUserId != currentUser?.Id)
            return Forbid();

        var oldStatus    = ticket.Status;
        ticket.Status    = TicketStatus.Closed;
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.ResolvedAt ??= DateTime.UtcNow;

        await _db.SaveChangesAsync();

        if (oldStatus != TicketStatus.Closed && isAdmin)
            await SendTicketStatusEmailAsync(ticket, "Suljettu");

        await _audit.LogAsync("TicketClosed", "Ticket", id.ToString());
        TempData["SuccessMessage"] = $"Tiketti #{id} suljettu.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── POST /Ticket/UploadAttachment ────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAttachment(int ticketId, IFormFile file)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var ticket = await _db.Tickets.FindAsync(ticketId);
        if (ticket is null) return NotFound();

        if (!isAdmin && ticket.CustomerId != currentUser?.CustomerId)
            return Forbid();

        if (currentUser is null) return Unauthorized();

        if (file is null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Tiedosto on tyhjä tai puuttuu.";
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        if (file.Length > 20 * 1024 * 1024)
        {
            TempData["ErrorMessage"] = "Tiedosto on liian suuri (max 20 MB).";
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".png", ".jpg", ".jpeg", ".gif", ".zip" };
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !allowedExt.Contains(ext))
        {
            TempData["ErrorMessage"] = $"Tiedostotyyppi '{ext}' ei ole sallittu.";
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        var filePath = await _fileStorage.SaveFileAsync(file, "tickets");

        _db.TicketAttachments.Add(new TicketAttachment
        {
            TicketId         = ticketId,
            FileName         = Path.GetFileName(file.FileName),
            FilePath         = filePath,
            UploadedAt       = DateTime.UtcNow,
            UploadedByUserId = currentUser.Id
        });
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Tiedosto '{Path.GetFileName(file.FileName)}' lisätty.";
        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    // ── GET /Ticket/DownloadAttachment/5 ─────────────────────────────
    [HttpGet]
    public async Task<IActionResult> DownloadAttachment(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var attachment = await _db.TicketAttachments
            .Include(a => a.Ticket)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attachment is null) return NotFound();

        if (!isAdmin && attachment.Ticket?.CustomerId != currentUser?.CustomerId)
            return Forbid();

        var fullPath = _fileStorage.ResolveSafePath(attachment.FilePath);
        if (fullPath is null || !System.IO.File.Exists(fullPath))
            return NotFound();

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(attachment.FileName, out var contentType))
            contentType = "application/octet-stream";

        return PhysicalFile(fullPath, contentType, attachment.FileName);
    }

    // ── POST /Ticket/DeleteAttachment/5 (Admin) ──────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttachment(int id)
    {
        var attachment = await _db.TicketAttachments.FindAsync(id);
        if (attachment is null) return NotFound();

        int ticketId = attachment.TicketId;
        _fileStorage.DeleteFile(attachment.FilePath);
        _db.TicketAttachments.Remove(attachment);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Liite poistettu.";
        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    // ── POST /Ticket/SubmitFeedback ──────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitFeedback(int ticketId, int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
            return BadRequest();

        var currentUser = await _userManager.GetUserAsync(User);
        var ticket      = await _db.Tickets
            .Include(t => t.Feedback)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket is null) return NotFound();
        if (ticket.Feedback is not null) return RedirectToAction(nameof(Details), new { id = ticketId });
        if (ticket.Status != TicketStatus.Closed) return RedirectToAction(nameof(Details), new { id = ticketId });
        if (ticket.CreatedByUserId != currentUser?.Id) return Forbid();

        _db.TicketFeedbacks.Add(new TicketFeedback
        {
            TicketId    = ticketId,
            UserId      = currentUser.Id,
            Rating      = rating,
            Comment     = comment?.Trim(),
            SubmittedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await _audit.LogAsync("FeedbackSubmitted", "Ticket", ticketId.ToString(), $"Rating: {rating}");

        TempData["SuccessMessage"] = "Kiitos palautteestasi!";
        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    // ── Apumetodit ───────────────────────────────────────────────────

    private async Task SendTicketStatusEmailAsync(Ticket ticket, string statusText)
    {
        if (ticket.CreatedByUser == null || string.IsNullOrWhiteSpace(ticket.CreatedByUser.Email))
            return;

        string name = !string.IsNullOrWhiteSpace(ticket.CreatedByUser.FullName)
            ? ticket.CreatedByUser.FullName
            : ticket.CreatedByUser.Email;

        string subject     = $"Tiketti #{ticket.Id} on {statusText.ToLower()}";
        string htmlMessage = $@"
            <div style=""font-family: Arial, sans-serif; color: #333;"">
                <h3 style=""color: #2b5797;"">Hei {name}!</h3>
                <p>Tukipyyntösi (Tiketti #{ticket.Id}: <strong>{ticket.Title}</strong>) tila on muuttunut.</p>
                <p>Uusi tila: <strong style=""padding: 3px 6px; background-color: #f1f5f9; border-radius: 4px;"">{statusText}</strong></p>
                <p>Kirjaudu HakaTech Portaaliin tarkastellaksesi tiketin tietoja ja mahdollisia vastauksia.</p>
                <br/><hr style=""border: none; border-top: 1px solid #ddd;""/><p style=""font-size: 0.9em; color: #777;"">Ystävällisin terveisin,<br/><strong>HakaTech Asiakastuki</strong></p>
            </div>";

        await _emailService.SendEmailAsync(ticket.CreatedByUser.Email, subject, htmlMessage);
    }

    private async Task<IEnumerable<SelectListItem>> BuildCustomerOptions() =>
        (await _db.Customers
            .Where(c => c.IsActive)
            .OrderBy(c => c.CompanyName)
            .ToListAsync())
        .Select(c => new SelectListItem(c.CompanyName, c.Id.ToString()));

    private async Task<TicketEditViewModel> BuildEditViewModel(Ticket ticket)
    {
        var staff = await _userManager.GetUsersInRoleAsync("Admin");
        return new TicketEditViewModel
        {
            Id               = ticket.Id,
            Status           = ticket.Status,
            Priority         = ticket.Priority,
            AssignedToUserId = ticket.AssignedToUserId,
            StaffOptions     = staff.Select(u =>
                new SelectListItem(u.FullName.Length > 0 ? u.FullName : u.Email, u.Id))
        };
    }
}
