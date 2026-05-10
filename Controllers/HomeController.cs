using System.Diagnostics;
using HakaTech.Portal.Data;
using HakaTech.Portal.Models;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

/// <summary>
/// Kotisivun (dashboardin) controller. Vastaa portaalin etusivusta,
/// käyttäjän mukautetun widget-järjestyksen tallentamisesta ja
/// virhe- ja yksityisyyssivuista.
/// </summary>
public class HomeController : Controller
{
    private readonly ApplicationDbContext         _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<HomeController>      _logger;

    public HomeController(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager,
        ILogger<HomeController>      logger)
    {
        _db          = db;
        _userManager = userManager;
        _logger      = logger;
    }

    /// <summary>
    /// GET / — Dashboard. Kerää roolikohtaiset luvut, listat ja sparkline-trendit.
    /// Adminille näytetään kaikki asiakkaat, asiakaskäyttäjälle vain oman yrityksen tiedot.
    /// </summary>
    [Authorize]
    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");
        var today       = DateTime.UtcNow.Date;
        var in7Days     = today.AddDays(7);

        // ── Widget-järjestys ───────────────────────────────────────────
        var defaultWidgets = isAdmin
            ? new[] { "kpi", "tickets", "invoices", "calendar", "quickactions" }
            : new[] { "kpi", "tickets", "invoices", "calendar", "quickactions" };

        var savedLayout = currentUser?.DashboardLayout;
        var widgetOrder = string.IsNullOrWhiteSpace(savedLayout)
            ? defaultWidgets.ToList()
            : savedLayout.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        var vm = new DashboardViewModel
        {
            IsAdmin     = isAdmin,
            WelcomeName = !string.IsNullOrWhiteSpace(currentUser?.FullName)
                            ? currentUser.FullName
                            : currentUser?.Email ?? string.Empty,
            WidgetOrder = widgetOrder
        };

        // ── Aktiiviset tiedotteet (kaikille käyttäjille) ───────────────
        vm.ActiveAnnouncements = await _db.Announcements
            .Where(a => a.IsPublished &&
                        (a.ValidFrom  == null || a.ValidFrom  <= DateTime.UtcNow) &&
                        (a.ValidUntil == null || a.ValidUntil >= DateTime.UtcNow))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        if (isAdmin)
        {
            // ── Asiakkaat ──────────────────────────────────────────
            vm.TotalCustomers  = await _db.Customers.CountAsync();
            vm.ActiveCustomers = await _db.Customers.CountAsync(c => c.IsActive);

            // ── Tiketit ────────────────────────────────────────────
            vm.TotalTickets      = await _db.Tickets.CountAsync();
            vm.OpenTickets       = await _db.Tickets.CountAsync(t => t.Status == TicketStatus.Open);
            vm.InProgressTickets = await _db.Tickets.CountAsync(t => t.Status == TicketStatus.InProgress);
            vm.ResolvedTickets   = await _db.Tickets.CountAsync(t => t.Status == TicketStatus.Resolved);
            vm.CriticalTickets   = await _db.Tickets.CountAsync(t =>
                t.Priority == TicketPriority.Critical &&
                t.Status != TicketStatus.Closed &&
                t.Status != TicketStatus.Resolved);
            vm.HighTickets = await _db.Tickets.CountAsync(t =>
                t.Priority == TicketPriority.High &&
                t.Status != TicketStatus.Closed &&
                t.Status != TicketStatus.Resolved);

            // Tilajakauma
            vm.TicketsByStatus = await _db.Tickets
                .GroupBy(t => t.Status)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            // ── Laskut ─────────────────────────────────────────────
            vm.TotalInvoices = await _db.Invoices.CountAsync();
            vm.OverdueCount  = await _db.Invoices.CountAsync(i =>
                i.Status == InvoiceStatus.Overdue ||
                (i.DueDate.Date < today &&
                 i.Status != InvoiceStatus.Paid &&
                 i.Status != InvoiceStatus.Draft));

            var unpaidInvoices = await _db.Invoices
                .Include(i => i.Lines)
                .Where(i => i.Status == InvoiceStatus.Unpaid ||
                            i.Status == InvoiceStatus.Sent   ||
                            i.Status == InvoiceStatus.Overdue)
                .ToListAsync();
            vm.UnpaidTotal = unpaidInvoices.Sum(i => i.TotalAmount);

            // ── Odottavat tarjouspyynnöt ───────────────────────────
            vm.PendingQuoteRequests = await _db.QuoteRequests
                .CountAsync(q => q.Status == QuoteRequestStatus.Pending);

            // ── Listat ─────────────────────────────────────────────
            vm.RecentOpenTickets = await _db.Tickets
                .Include(t => t.Customer)
                .Include(t => t.AssignedToUser)
                .Where(t => t.Status == TicketStatus.Open ||
                            t.Status == TicketStatus.InProgress ||
                            t.Status == TicketStatus.WaitingCustomer)
                .OrderBy(t => t.Priority)    // Critical ensin (pienin enum-arvo ensin)
                .ThenByDescending(t => t.CreatedAt)
                .Take(8)
                .ToListAsync();

            vm.UpcomingDueInvoices = await _db.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Lines)
                .Where(i => i.DueDate.Date >= today &&
                            i.DueDate.Date <= in7Days &&
                            i.Status != InvoiceStatus.Paid &&
                            i.Status != InvoiceStatus.Draft)
                .OrderBy(i => i.DueDate)
                .Take(5)
                .ToListAsync();

            vm.OverdueInvoices = await _db.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Lines)
                .Where(i => i.DueDate.Date < today &&
                            i.Status != InvoiceStatus.Paid &&
                            i.Status != InvoiceStatus.Draft)
                .OrderBy(i => i.DueDate)
                .Take(5)
                .ToListAsync();
        }
        else
        {
            // ── Asiakaskäyttäjä: vain oman yrityksen data ──────────
            int? custId = currentUser?.CustomerId;
            if (custId.HasValue)
            {
                vm.OpenTickets = await _db.Tickets.CountAsync(t =>
                    t.CustomerId == custId &&
                    (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress));

                vm.RecentOpenTickets = await _db.Tickets
                    .Where(t => t.CustomerId == custId &&
                                t.Status != TicketStatus.Closed &&
                                t.Status != TicketStatus.Resolved)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                var myInvoices = await _db.Invoices
                    .Include(i => i.Lines)
                    .Where(i => i.CustomerId == custId &&
                                i.Status != InvoiceStatus.Paid &&
                                i.Status != InvoiceStatus.Draft)
                    .ToListAsync();

                vm.TotalInvoices  = myInvoices.Count;
                vm.UnpaidTotal    = myInvoices.Sum(i => i.TotalAmount);
                vm.OverdueCount   = myInvoices.Count(i => i.DueDate.Date < today);

                vm.UpcomingDueInvoices = await _db.Invoices
                    .Include(i => i.Lines)
                    .Where(i => i.CustomerId == custId &&
                                i.DueDate.Date >= today &&
                                i.DueDate.Date <= in7Days &&
                                i.Status != InvoiceStatus.Paid &&
                                i.Status != InvoiceStatus.Draft)
                    .OrderBy(i => i.DueDate)
                    .Take(5)
                    .ToListAsync();

                // ── Uudet lähetetyt laskut (viimeiset 30 pv) ──────────
                var since30Days = today.AddDays(-30);
                vm.NewSentInvoices = await _db.Invoices
                    .Include(i => i.Lines)
                    .Where(i => i.CustomerId == custId &&
                                i.Status == InvoiceStatus.Sent &&
                                i.InvoiceDate.Date >= since30Days)
                    .OrderByDescending(i => i.InvoiceDate)
                    .Take(5)
                    .ToListAsync();
            }
        }

        // ── Sparkline-sarjat viimeiseltä 14 päivältä ──────────────────
        var sparkFrom = today.AddDays(-13);
        var days = Enumerable.Range(0, 14).Select(i => sparkFrom.AddDays(i)).ToList();
        int? scopeCustomerId = isAdmin ? null : currentUser?.CustomerId;

        var ticketsCreated = await _db.Tickets
            .Where(t => t.CreatedAt >= sparkFrom &&
                        (scopeCustomerId == null || t.CustomerId == scopeCustomerId))
            .Select(t => t.CreatedAt.Date)
            .ToListAsync();
        vm.TicketsCreatedSpark = days.Select(d => (double)ticketsCreated.Count(x => x == d)).ToArray();

        var invoicesIssued = await _db.Invoices
            .Where(i => i.InvoiceDate >= sparkFrom &&
                        i.Status != InvoiceStatus.Draft &&
                        (scopeCustomerId == null || i.CustomerId == scopeCustomerId))
            .Select(i => new { i.InvoiceDate, i.TotalAmount })
            .ToListAsync();
        vm.InvoicesIssuedSpark = days.Select(d => (double)invoicesIssued.Count(x => x.InvoiceDate.Date == d)).ToArray();
        vm.UnpaidTotalSpark    = days.Select(d => (double)invoicesIssued
                                                          .Where(x => x.InvoiceDate.Date <= d)
                                                          .Sum(x => x.TotalAmount)).ToArray();
        // Open-tickets spark: running count of currently-open tickets by created<=d & (resolved null or > d)
        vm.TicketsOpenSpark = days.Select(_ => (double)vm.OpenTickets).ToArray();

        // ── Tulevat varaukset (kalenteriwidget) ────────────────────────
        var nowLocal = DateTime.Now;
        var in14Days = nowLocal.AddDays(14);
        vm.UpcomingBookingSlots = await _db.BookingSlots
            .Include(s => s.Bookings)
            .Where(s => s.IsActive && s.StartTime >= nowLocal && s.StartTime <= in14Days)
            .OrderBy(s => s.StartTime)
            .Take(6)
            .ToListAsync();

        return View(vm);
    }

    /// <summary>
    /// POST /Home/SaveLayout — Tallentaa käyttäjän mukautetun widget-järjestyksen.
    /// Selain lähettää avaimien taulukon (esim. ["tickets", "kpi"]).
    /// Validoidaan että avaimet ovat tunnettuja, jotta tietokantaan ei pääse roskaa.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SaveLayout([FromBody] string[] order)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        // Sallitut widget-avaimet — kaikki muu suodatetaan pois.
        var allowed = new HashSet<string> { "kpi", "tickets", "invoices", "calendar", "quickactions" };
        var clean   = order.Where(w => allowed.Contains(w)).Distinct().ToArray();

        // Tallennetaan pilkulla erotettuna merkkijonona.
        user.DashboardLayout = string.Join(",", clean);
        await _userManager.UpdateAsync(user);
        return Ok();
    }

    /// <summary>GET /Home/Privacy — staattinen tietosuojaseloste-sivu.</summary>
    [AllowAnonymous]
    public IActionResult Privacy() => View();

    /// <summary>
    /// GET /Home/Error — virhesivu. Välitetään pyyntötunnus,
    /// jolla virhe voidaan jäljittää lokeista.
    /// ResponseCache estää välimuistituksen — virhesivua ei saa cacheta.
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
