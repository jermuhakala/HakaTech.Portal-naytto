using System.Globalization;
using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using HakaTech.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

/// <summary>
/// Huoltokalenterin controller. Asiakas näkee vapaita aikaikkunoita
/// (BookingSlot) ja voi tehdä varauksen (Booking). Admin luo ja hallinnoi
/// aikaikkunoita sekä vahvistaa varaukset.
/// </summary>
[Authorize]
public class BookingController : Controller
{
    private readonly ApplicationDbContext          _db;
    private readonly UserManager<ApplicationUser>  _userManager;
    private readonly IEmailService                 _emailService;
    private readonly IAuditService                 _audit;
    private readonly ILogger<BookingController>    _logger;

    private static readonly CultureInfo FI = new("fi-FI");

    public BookingController(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager,
        IEmailService                emailService,
        IAuditService                audit,
        ILogger<BookingController>   logger)
    {
        _db          = db;
        _userManager = userManager;
        _emailService = emailService;
        _audit       = audit;
        _logger      = logger;
    }

    // ── GET /Booking ─────────────────────────────────────────────────
    public async Task<IActionResult> Index(int? year, int? month, int? day, BookingSlotType? service)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var now = DateTime.Now;
        int y = year .HasValue ? Math.Clamp(year.Value,  2020, 2035) : now.Year;
        int m = month.HasValue ? Math.Clamp(month.Value, 1,    12)   : now.Month;

        var monthStart = new DateTime(y, m, 1);
        var monthEnd   = monthStart.AddMonths(1);

        var slotsQuery = _db.BookingSlots
            .Include(s => s.Bookings).ThenInclude(b => b.User)
            .Include(s => s.Bookings).ThenInclude(b => b.Customer)
            .Where(s => s.StartTime >= monthStart && s.StartTime < monthEnd);

        if (!isAdmin)
            slotsQuery = slotsQuery.Where(s => s.IsActive);

        if (service.HasValue)
            slotsQuery = slotsQuery.Where(s => s.SlotType == service.Value);

        var slots = await slotsQuery.OrderBy(s => s.StartTime).ToListAsync();

        var myBookedSlotIds = new HashSet<int>();
        if (!isAdmin && currentUser is not null)
        {
            var ids = await _db.Bookings
                .Where(b => b.UserId == currentUser.Id
                         && b.Status != BookingStatus.Cancelled)
                .Select(b => b.BookingSlotId)
                .ToListAsync();
            myBookedSlotIds = ids.ToHashSet();
        }

        var vm = new BookingCalendarViewModel
        {
            Year              = y,
            Month             = m,
            SelectedDay       = day,
            ServiceTypeFilter = service,
            AllSlots          = slots,
            MyBookedSlotIds   = myBookedSlotIds,
            IsAdmin           = isAdmin
        };

        return View(vm);
    }

    // ── GET /Booking/Book/5 ──────────────────────────────────────────
    public async Task<IActionResult> Book(int id)
    {
        if (User.IsInRole("Admin"))
            return RedirectToAction(nameof(ManageSlots));

        var currentUser = await _userManager.GetUserAsync(User);

        var slot = await _db.BookingSlots
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (slot is null) return NotFound();

        if (!slot.IsAvailable)
        {
            TempData["ErrorMessage"] = "Aikaväli ei ole enää varattavissa.";
            return RedirectToAction(nameof(Index),
                new { year = slot.StartTime.Year, month = slot.StartTime.Month });
        }

        bool alreadyBooked = currentUser is not null && await _db.Bookings.AnyAsync(
            b => b.BookingSlotId == id
              && b.UserId == currentUser.Id
              && b.Status != BookingStatus.Cancelled);

        if (alreadyBooked)
        {
            TempData["ErrorMessage"] = "Sinulla on jo varaus tälle aikavälle.";
            return RedirectToAction(nameof(Index),
                new { year = slot.StartTime.Year, month = slot.StartTime.Month });
        }

        ViewBag.Slot = slot;
        return View(new BookingFormViewModel { BookingSlotId = id });
    }

    // ── POST /Booking/Book ───────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookingFormViewModel model)
    {
        if (User.IsInRole("Admin"))
            return RedirectToAction(nameof(ManageSlots));

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.CustomerId is null) return Forbid();

        var slot = await _db.BookingSlots
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.Id == model.BookingSlotId);

        if (slot is null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.Slot = slot;
            return View(model);
        }

        if (!slot.IsAvailable)
        {
            TempData["ErrorMessage"] = "Aikaväli ei ole enää varattavissa.";
            return RedirectToAction(nameof(Index),
                new { year = slot.StartTime.Year, month = slot.StartTime.Month });
        }

        bool alreadyBooked = await _db.Bookings.AnyAsync(
            b => b.BookingSlotId == model.BookingSlotId
              && b.UserId == currentUser.Id
              && b.Status != BookingStatus.Cancelled);

        if (alreadyBooked)
        {
            TempData["ErrorMessage"] = "Sinulla on jo varaus tälle aikavälle.";
            return RedirectToAction(nameof(Index),
                new { year = slot.StartTime.Year, month = slot.StartTime.Month });
        }

        var booking = new Booking
        {
            BookingSlotId = model.BookingSlotId,
            CustomerId    = currentUser.CustomerId.Value,
            UserId        = currentUser.Id,
            Notes         = model.Notes?.Trim(),
            Status        = BookingStatus.Pending,
            CreatedAt     = DateTime.UtcNow
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("BookingCreated", "Booking", booking.Id.ToString(),
            $"{slot.Title} / {slot.StartTime:dd.MM.yyyy HH:mm}");

        // Sähköposti-ilmoitus
        try
        {
            var customer = await _db.Customers.FindAsync(currentUser.CustomerId.Value);
            if (!string.IsNullOrWhiteSpace(currentUser.Email))
            {
                await _emailService.SendEmailAsync(
                    currentUser.Email,
                    $"Varausvahvistus – {slot.Title}",
                    BookingRequestEmail(slot, currentUser.FullName ?? currentUser.Email,
                                        customer?.CompanyName ?? ""));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Varausvahvistuksen lähetys epäonnistui.");
        }

        TempData["SuccessMessage"] =
            $"Varaus vastaanotettu: {slot.Title} {slot.StartTime:dd.MM.yyyy HH:mm}. " +
            "Saat vahvistuksen sähköpostiin.";
        return RedirectToAction(nameof(MyBookings));
    }

    // ── POST /Booking/Cancel/5 ───────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var booking = await _db.Bookings
            .Include(b => b.BookingSlot)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null) return NotFound();

        if (!isAdmin && booking.UserId != currentUser?.Id)
            return Forbid();

        if (booking.Status == BookingStatus.Cancelled)
        {
            TempData["ErrorMessage"] = "Varaus on jo peruutettu.";
            return isAdmin
                ? RedirectToAction(nameof(ManageBookings))
                : RedirectToAction(nameof(MyBookings));
        }

        booking.Status             = BookingStatus.Cancelled;
        booking.CancelledAt        = DateTime.UtcNow;
        booking.CancellationReason = reason?.Trim();
        await _db.SaveChangesAsync();

        await _audit.LogAsync("BookingCancelled", "Booking", id.ToString(),
            booking.BookingSlot?.Title);

        TempData["SuccessMessage"] = "Varaus peruutettu.";
        return isAdmin
            ? RedirectToAction(nameof(ManageBookings))
            : RedirectToAction(nameof(MyBookings));
    }

    // ── GET /Booking/MyBookings ──────────────────────────────────────
    public async Task<IActionResult> MyBookings()
    {
        if (User.IsInRole("Admin"))
            return RedirectToAction(nameof(ManageBookings));

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        var now = DateTime.Now;

        var bookings = await _db.Bookings
            .Include(b => b.BookingSlot)
            .Where(b => b.UserId == currentUser.Id)
            .OrderByDescending(b => b.BookingSlot!.StartTime)
            .ToListAsync();

        var vm = new BookingMyViewModel
        {
            Upcoming = bookings
                .Where(b => b.Status != BookingStatus.Cancelled
                         && b.BookingSlot?.StartTime >= now)
                .ToList(),
            Past = bookings
                .Where(b => b.Status == BookingStatus.Cancelled
                         || b.BookingSlot?.StartTime < now)
                .ToList()
        };

        return View(vm);
    }

    // ════════════════════════════════════════════════════════════════
    // ADMIN
    // ════════════════════════════════════════════════════════════════

    // ── GET /Booking/ManageSlots ─────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ManageSlots(int? year, int? month)
    {
        var now = DateTime.Now;
        int y   = year .HasValue ? Math.Clamp(year.Value,  2020, 2035) : now.Year;
        int m   = month.HasValue ? Math.Clamp(month.Value, 1,    12)   : now.Month;

        var monthStart = new DateTime(y, m, 1);
        var monthEnd   = monthStart.AddMonths(1);

        var slots = await _db.BookingSlots
            .Include(s => s.Bookings).ThenInclude(b => b.User)
            .Where(s => s.StartTime >= monthStart && s.StartTime < monthEnd)
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        var (py, pm) = m == 1  ? (y - 1, 12) : (y, m - 1);
        var (ny, nm) = m == 12 ? (y + 1,  1) : (y, m + 1);

        ViewBag.Year      = y;
        ViewBag.Month     = m;
        ViewBag.MonthName = monthStart.ToString("MMMM yyyy", FI);
        ViewBag.PrevYear  = py; ViewBag.PrevMonth = pm;
        ViewBag.NextYear  = ny; ViewBag.NextMonth = nm;

        return View(slots);
    }

    // ── GET /Booking/CreateSlot ──────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public IActionResult CreateSlot(DateTime? date)
    {
        var def = date?.Date.AddHours(9) ?? DateTime.Now.Date.AddDays(1).AddHours(9);
        ViewBag.FormAction = "CreateSlot";
        return View("SlotForm", new BookingSlotFormViewModel { StartTime = def });
    }

    // ── POST /Booking/CreateSlot ─────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateSlot(BookingSlotFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.FormAction = "CreateSlot";
            return View("SlotForm", model);
        }

        var currentUser = await _userManager.GetUserAsync(User);

        var slot = new BookingSlot
        {
            Title           = model.Title.Trim(),
            Description     = model.Description?.Trim(),
            SlotType        = model.SlotType,
            StartTime       = model.StartTime,
            DurationMinutes = model.DurationMinutes,
            MaxCapacity     = model.MaxCapacity,
            IsActive        = model.IsActive,
            CreatedByUserId = currentUser?.Id
        };

        _db.BookingSlots.Add(slot);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("SlotCreated", "BookingSlot", slot.Id.ToString(), slot.Title);

        TempData["SuccessMessage"] = $"Aikaväli '{slot.Title}' luotu.";
        return RedirectToAction(nameof(Index),
            new { year = slot.StartTime.Year, month = slot.StartTime.Month });
    }

    // ── GET /Booking/EditSlot/5 ──────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditSlot(int id)
    {
        var slot = await _db.BookingSlots.FindAsync(id);
        if (slot is null) return NotFound();

        ViewBag.FormAction = "EditSlot";
        return View("SlotForm", new BookingSlotFormViewModel
        {
            Id              = slot.Id,
            Title           = slot.Title,
            Description     = slot.Description,
            SlotType        = slot.SlotType,
            StartTime       = slot.StartTime,
            DurationMinutes = slot.DurationMinutes,
            MaxCapacity     = slot.MaxCapacity,
            IsActive        = slot.IsActive
        });
    }

    // ── POST /Booking/EditSlot ───────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditSlot(BookingSlotFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.FormAction = "EditSlot";
            return View("SlotForm", model);
        }

        var slot = await _db.BookingSlots.FindAsync(model.Id);
        if (slot is null) return NotFound();

        slot.Title           = model.Title.Trim();
        slot.Description     = model.Description?.Trim();
        slot.SlotType        = model.SlotType;
        slot.StartTime       = model.StartTime;
        slot.DurationMinutes = model.DurationMinutes;
        slot.MaxCapacity     = model.MaxCapacity;
        slot.IsActive        = model.IsActive;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("SlotUpdated", "BookingSlot", slot.Id.ToString(), slot.Title);

        TempData["SuccessMessage"] = $"Aikaväli '{slot.Title}' päivitetty.";
        return RedirectToAction(nameof(Index),
            new { year = slot.StartTime.Year, month = slot.StartTime.Month });
    }

    // ── POST /Booking/DeleteSlot/5 ───────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSlot(int id)
    {
        var slot = await _db.BookingSlots
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (slot is null) return NotFound();

        if (slot.Bookings.Any(b => b.Status != BookingStatus.Cancelled))
        {
            TempData["ErrorMessage"] =
                "Aikaväliä ei voi poistaa — sillä on aktiivisia varauksia.";
            return RedirectToAction(nameof(Index),
                new { year = slot.StartTime.Year, month = slot.StartTime.Month });
        }

        _db.BookingSlots.Remove(slot);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("SlotDeleted", "BookingSlot", id.ToString(), slot.Title);

        TempData["SuccessMessage"] = $"Aikaväli '{slot.Title}' poistettu.";
        return RedirectToAction(nameof(Index));
    }

    // ── GET /Booking/ManageBookings ──────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ManageBookings(BookingStatus? status, int? customerId)
    {
        var query = _db.Bookings
            .Include(b => b.BookingSlot)
            .Include(b => b.User)
            .Include(b => b.Customer)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        if (customerId.HasValue)
            query = query.Where(b => b.CustomerId == customerId.Value);

        var bookings = await query
            .OrderByDescending(b => b.BookingSlot!.StartTime)
            .ToListAsync();

        ViewBag.StatusFilter   = status;
        ViewBag.CustomerFilter = customerId;
        ViewBag.Customers = await _db.Customers
            .Where(c => c.IsActive).OrderBy(c => c.CompanyName).ToListAsync();

        return View(bookings);
    }

    // ── POST /Booking/ConfirmBooking/5 ───────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> ConfirmBooking(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.BookingSlot)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null) return NotFound();

        booking.Status = BookingStatus.Confirmed;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("BookingConfirmed", "Booking", id.ToString(),
            booking.BookingSlot?.Title);

        // Vahvistussähköposti asiakkaalle
        try
        {
            if (!string.IsNullOrWhiteSpace(booking.User?.Email))
            {
                await _emailService.SendEmailAsync(
                    booking.User.Email,
                    $"Varauksesi vahvistettu – {booking.BookingSlot?.Title}",
                    BookingConfirmedEmail(booking.BookingSlot!,
                                          booking.User.FullName ?? booking.User.Email));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vahvistussähköpostin lähetys epäonnistui.");
        }

        TempData["SuccessMessage"] = "Varaus vahvistettu ja asiakkaalle lähetetty tieto.";
        return RedirectToAction(nameof(ManageBookings));
    }

    // ── Sähköpostipohjat ─────────────────────────────────────────────
    private static string BookingRequestEmail(
        BookingSlot slot, string userName, string companyName) => $"""
        <div style="font-family:Inter,Arial,sans-serif;max-width:600px;margin:0 auto;color:#1e293b">
          <div style="background:#2563eb;padding:24px 32px;border-radius:8px 8px 0 0">
            <h1 style="color:#fff;margin:0;font-size:22px">HakaTech – Varausvahvistus</h1>
          </div>
          <div style="background:#f8fafc;padding:24px 32px;border-radius:0 0 8px 8px;border:1px solid #e2e8f0">
            <p style="margin:0 0 16px">Hei <strong>{userName}</strong>,</p>
            <p style="margin:0 0 16px">Varauksesi on vastaanotettu. Vahvistamme sen pian.</p>
            <table style="width:100%;border-collapse:collapse;margin-bottom:20px">
              <tr><td style="padding:8px 0;color:#64748b;width:140px">Palvelu</td>
                  <td style="padding:8px 0;font-weight:600">{slot.Title}</td></tr>
              <tr><td style="padding:8px 0;color:#64748b">Ajankohta</td>
                  <td style="padding:8px 0;font-weight:600">{slot.StartTime:dd.MM.yyyy HH:mm} – {slot.EndTime:HH:mm}</td></tr>
              <tr><td style="padding:8px 0;color:#64748b">Kesto</td>
                  <td style="padding:8px 0">{slot.DurationMinutes} min</td></tr>
              <tr><td style="padding:8px 0;color:#64748b">Yritys</td>
                  <td style="padding:8px 0">{companyName}</td></tr>
            </table>
            <p style="margin:0;color:#94a3b8;font-size:13px">
              HakaTech IT-palvelut | asiakastuki@hakatech.fi
            </p>
          </div>
        </div>
        """;

    private static string BookingConfirmedEmail(BookingSlot slot, string userName) => $"""
        <div style="font-family:Inter,Arial,sans-serif;max-width:600px;margin:0 auto;color:#1e293b">
          <div style="background:#16a34a;padding:24px 32px;border-radius:8px 8px 0 0">
            <h1 style="color:#fff;margin:0;font-size:22px">HakaTech – Varaus vahvistettu ✓</h1>
          </div>
          <div style="background:#f8fafc;padding:24px 32px;border-radius:0 0 8px 8px;border:1px solid #e2e8f0">
            <p style="margin:0 0 16px">Hei <strong>{userName}</strong>,</p>
            <p style="margin:0 0 16px">Varauksesi on vahvistettu!</p>
            <table style="width:100%;border-collapse:collapse;margin-bottom:20px">
              <tr><td style="padding:8px 0;color:#64748b;width:140px">Palvelu</td>
                  <td style="padding:8px 0;font-weight:600">{slot.Title}</td></tr>
              <tr><td style="padding:8px 0;color:#64748b">Ajankohta</td>
                  <td style="padding:8px 0;font-weight:600;color:#16a34a">{slot.StartTime:dd.MM.yyyy HH:mm} – {slot.EndTime:HH:mm}</td></tr>
              <tr><td style="padding:8px 0;color:#64748b">Kesto</td>
                  <td style="padding:8px 0">{slot.DurationMinutes} min</td></tr>
            </table>
            <p style="margin:0;color:#94a3b8;font-size:13px">
              HakaTech IT-palvelut | asiakastuki@hakatech.fi
            </p>
          </div>
        </div>
        """;
}
