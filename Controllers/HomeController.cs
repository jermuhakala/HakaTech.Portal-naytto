// Nimiavaruuksien tuonnit.
using System.Diagnostics;                          // Activity.Current — pyyntötunniste virhesivulle.
using HakaTech.Portal.Data;                        // ApplicationDbContext — tietokantayhteys.
using HakaTech.Portal.Models;                      // ErrorViewModel — virhesivun malli.
using HakaTech.Portal.Models.Domain;               // TicketStatus, InvoiceStatus, QuoteRequestStatus...
using HakaTech.Portal.Models.ViewModels;           // DashboardViewModel.
using Microsoft.AspNetCore.Authorization;          // [Authorize], [AllowAnonymous].
using Microsoft.AspNetCore.Identity;               // UserManager.
using Microsoft.AspNetCore.Mvc;                    // Controller, IActionResult...
using Microsoft.EntityFrameworkCore;               // ToListAsync, CountAsync, ToDictionaryAsync...

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Kotisivun (dashboardin) controller. Vastaa portaalin etusivusta,
/// käyttäjän mukautetun widget-järjestyksen tallentamisesta ja
/// virhe- ja yksityisyyssivuista.
/// </summary>
// Ei [Authorize]-attribuuttia controllerin tasolla — se on yksittäisissä metodeissa,
// koska Error- ja Privacy-sivut ovat julkisia.
public class HomeController : Controller
{
    // Tietokantayhteys — injektoitu konstruktorissa.
    private readonly ApplicationDbContext         _db;
    // UserManager — haetaan kirjautunut käyttäjä rooleineen.
    private readonly UserManager<ApplicationUser> _userManager;
    // Diagnostiikkaloki kehittäjälle.
    private readonly ILogger<HomeController>      _logger;

    // Konstruktori: DI-säiliö täyttää parametrit.
    public HomeController(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager,
        ILogger<HomeController>      logger)
    {
        _db          = db;
        _userManager = userManager;
        _logger      = logger;
    }

    // ── GET / (tai /Home/Index) ──────────────────────────────────────────────
    /// <summary>
    /// Dashboard. Kerää roolikohtaiset luvut, listat ja sparkline-trendit.
    /// Adminille näytetään kaikki asiakkaat, asiakaskäyttäjälle vain oman yrityksen tiedot.
    /// </summary>
    // [Authorize] = kirjautuminen vaaditaan — kirjautumaton ohjataan kirjautumissivulle.
    [Authorize]
    public async Task<IActionResult> Index()
    {
        // Haetaan kirjautunut käyttäjä tietokannasta (koko käyttäjäobjekti, ei vain väittämät).
        var currentUser = await _userManager.GetUserAsync(User);
        // Tarkistetaan onko käyttäjä admin — admin ja asiakas näkevät eri datan.
        bool isAdmin = User.IsInRole("Admin");
        // Tänään UTC-muodossa — käytetään eräpäivävertailuihin.
        var today   = DateTime.UtcNow.Date;
        // 7 päivää eteenpäin — "erääntyvät pian" -suodatukseen.
        var in7Days = today.AddDays(7);

        // ── Widget-järjestys ─────────────────────────────────────────────────
        // Oletuswidgetit jos käyttäjä ei ole tallentanut omaa järjestystä.
        // Tällä hetkellä admin ja asiakas saavat saman järjestyksen —
        // eriyttäminen onnistuu muuttamalla admin-haaran arvoja.
        var defaultWidgets = isAdmin
            ? new[] { "kpi", "tickets", "invoices", "calendar", "quickactions" }
            : new[] { "kpi", "tickets", "invoices", "calendar", "quickactions" };

        // Haetaan tallennettu järjestys tietokannasta (pilkulla erotettu merkkijono).
        var savedLayout = currentUser?.DashboardLayout;
        // Jos tallennettua järjestystä ei ole, käytetään oletusta.
        // Split(',', RemoveEmptyEntries) = pilkotaan merkkijono listaksi,
        //   RemoveEmptyEntries = jätetään pois tyhjät osat (esim. "a,,b" → ["a","b"]).
        var widgetOrder = string.IsNullOrWhiteSpace(savedLayout)
            ? defaultWidgets.ToList()
            : savedLayout.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Rakennetaan DashboardViewModel — se täytetään alla roolikohtaisesti.
        var vm = new DashboardViewModel
        {
            IsAdmin     = isAdmin,
            // Tervetuloa-nimi: ensisijaisesti koko nimi, toissijaisesti sähköposti, viimeisenä tyhjä.
            // "??" = null-yhdistämisoperaattori: käytetään seuraavaa arvoa jos vasen puoli on null.
            WelcomeName = !string.IsNullOrWhiteSpace(currentUser?.FullName)
                            ? currentUser.FullName
                            : currentUser?.Email ?? string.Empty,
            WidgetOrder = widgetOrder  // Widgettien piirtojärjestys näkymässä.
        };

        // ── Aktiiviset tiedotteet (kaikille käyttäjille) ─────────────────────
        // Näytetään tiedotteet, jotka ovat julkaistu JA ovat voimassaoloajalla.
        // ValidFrom == null = ei alkamispäivää → tiedote on heti voimassa.
        // ValidUntil == null = ei loppumispäivää → tiedote on aina voimassa.
        vm.ActiveAnnouncements = await _db.Announcements
            .Where(a => a.IsPublished &&
                        (a.ValidFrom  == null || a.ValidFrom  <= DateTime.UtcNow) &&
                        (a.ValidUntil == null || a.ValidUntil >= DateTime.UtcNow))
            .OrderByDescending(a => a.CreatedAt) // Uusin ensin.
            .ToListAsync();

        if (isAdmin)
        {
            // ════════════════════════════════════════════════════════════════
            // ADMIN-DASHBOARD: kaikki asiakkaat, tiketit ja laskut koko portaalissa.
            // ════════════════════════════════════════════════════════════════

            // ── Asiakkaiden lukumäärät ────────────────────────────────────
            // CountAsync() = SQL:n COUNT(*) — laskee taulun rivien määrän ilman lataamista.
            vm.TotalCustomers  = await _db.Customers.CountAsync();
            vm.ActiveCustomers = await _db.Customers.CountAsync(c => c.IsActive);

            // ── Tikettien lukumäärät ──────────────────────────────────────
            vm.TotalTickets      = await _db.Tickets.CountAsync();
            // Avoimet tiketit = Status on Open.
            vm.OpenTickets       = await _db.Tickets.CountAsync(t => t.Status == TicketStatus.Open);
            // Työn alla = Status on InProgress.
            vm.InProgressTickets = await _db.Tickets.CountAsync(t => t.Status == TicketStatus.InProgress);
            // Ratkaistut = Status on Resolved (muttei vielä suljettu).
            vm.ResolvedTickets   = await _db.Tickets.CountAsync(t => t.Status == TicketStatus.Resolved);
            // Kriittiset tiketit: Priority=Critical JA ei suljettu tai ratkaistu.
            // Nämä tarvitsevat välitöntä huomiota.
            vm.CriticalTickets   = await _db.Tickets.CountAsync(t =>
                t.Priority == TicketPriority.Critical &&
                t.Status != TicketStatus.Closed &&
                t.Status != TicketStatus.Resolved);
            // Korkean prioriteetin tiketit.
            vm.HighTickets = await _db.Tickets.CountAsync(t =>
                t.Priority == TicketPriority.High &&
                t.Status != TicketStatus.Closed &&
                t.Status != TicketStatus.Resolved);

            // Tilajakauma piirakkakaaviolle.
            // GroupBy(t => t.Status) = SQL:n GROUP BY Status.
            // ToDictionaryAsync(g => g.Key, g => g.Count()) = muodostaa hakemiston Status → lukumäärä.
            // Esim. { Open: 12, InProgress: 5, Resolved: 3, Closed: 20 }
            vm.TicketsByStatus = await _db.Tickets
                .GroupBy(t => t.Status)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            // ── Laskujen tiedot ───────────────────────────────────────────
            vm.TotalInvoices = await _db.Invoices.CountAsync();
            // Erääntyneet laskut: Status on Overdue TAI eräpäivä on menneisyydessä
            // eikä lasku ole maksettu tai luonnos.
            vm.OverdueCount  = await _db.Invoices.CountAsync(i =>
                i.Status == InvoiceStatus.Overdue ||
                (i.DueDate.Date < today &&
                 i.Status != InvoiceStatus.Paid &&
                 i.Status != InvoiceStatus.Draft));

            // Maksamattomien laskujen yhteissumma.
            // Ladataan rivit muistiin koska Sum(i => i.TotalAmount) ei onnistu
            // suoraan SQL:ssa (TotalAmount on laskettu ominaisuus, ei tietokantakenttä).
            var unpaidInvoices = await _db.Invoices
                .Include(i => i.Lines) // Rivit tarvitaan TotalAmount-laskentaan.
                .Where(i => i.Status == InvoiceStatus.Unpaid ||
                            i.Status == InvoiceStatus.Sent   ||
                            i.Status == InvoiceStatus.Overdue)
                .ToListAsync();
            // Sum() = yhteenlasku kaikista maksamattomista laskuista.
            vm.UnpaidTotal = unpaidInvoices.Sum(i => i.TotalAmount);

            // ── Odottavat tarjouspyynnöt ──────────────────────────────────
            // Tarjouspyynnöt jotka odottavat adminin vastausta.
            vm.PendingQuoteRequests = await _db.QuoteRequests
                .CountAsync(q => q.Status == QuoteRequestStatus.Pending);

            // ── Äskettäiset avoinna olevat tiketit ───────────────────────
            // Näytetään adminin dashboardilla pikalistana.
            // Include() lataa navigaatio-ominaisuudet — customer.CompanyName ja
            // assignedUser.FullName tarvitaan näkymässä.
            vm.RecentOpenTickets = await _db.Tickets
                .Include(t => t.Customer)
                .Include(t => t.AssignedToUser)
                .Where(t => t.Status == TicketStatus.Open ||
                            t.Status == TicketStatus.InProgress ||
                            t.Status == TicketStatus.WaitingCustomer)
                // Kriittisimmät ensin: enum-arvot 0=Critical, 1=High, 2=Medium, 3=Low.
                // OrderBy(Priority) = pienin enum-arvo (kriittisin) tulee ensin.
                .OrderBy(t => t.Priority)
                .ThenByDescending(t => t.CreatedAt) // Sama prioriteetti → uusin ensin.
                .Take(8) // Korkeintaan 8 tikettiä dashboardille.
                .ToListAsync();

            // ── Pian erääntyvät laskut (seuraavat 7 päivää) ──────────────
            vm.UpcomingDueInvoices = await _db.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Lines)
                .Where(i => i.DueDate.Date >= today &&        // Eräpäivä ei ole vielä mennyt.
                            i.DueDate.Date <= in7Days &&      // Erääntyy seuraavan viikon aikana.
                            i.Status != InvoiceStatus.Paid && // Ei jo maksettu.
                            i.Status != InvoiceStatus.Draft)  // Ei luonnos (ei lähetetty asiakkaalle).
                .OrderBy(i => i.DueDate) // Lähinnä erääntyy ensin.
                .Take(5)
                .ToListAsync();

            // ── Erääntyneet laskut (maksamatta, eräpäivä mennyt) ─────────
            vm.OverdueInvoices = await _db.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Lines)
                .Where(i => i.DueDate.Date < today &&         // Eräpäivä menneisyydessä.
                            i.Status != InvoiceStatus.Paid && // Ei maksettu.
                            i.Status != InvoiceStatus.Draft)  // Ei luonnos.
                .OrderBy(i => i.DueDate) // Vanhin eräpäivä ensin (kiireisimmät ensin).
                .Take(5)
                .ToListAsync();
        }
        else
        {
            // ════════════════════════════════════════════════════════════════
            // ASIAKASKÄYTTÄJÄ: näytetään vain oman yrityksen data.
            // ════════════════════════════════════════════════════════════════

            // CustomerId yhdistää käyttäjän yritykseen.
            // null jos käyttäjä ei ole asiakasyrityksen jäsen (ei pitäisi tapahtua normaalisti).
            int? custId = currentUser?.CustomerId;
            if (custId.HasValue)
            {
                // Oman yrityksen avoinna olevat tiketit — counter dashboardin KPI-widgetille.
                vm.OpenTickets = await _db.Tickets.CountAsync(t =>
                    t.CustomerId == custId &&
                    (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress));

                // Äskeiset avoimet tiketit pikalistaksi.
                vm.RecentOpenTickets = await _db.Tickets
                    .Where(t => t.CustomerId == custId &&
                                t.Status != TicketStatus.Closed &&  // Ei suljettuja.
                                t.Status != TicketStatus.Resolved)  // Ei ratkaistuja.
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                // Oman yrityksen maksamattomat laskut.
                var myInvoices = await _db.Invoices
                    .Include(i => i.Lines)
                    .Where(i => i.CustomerId == custId &&
                                i.Status != InvoiceStatus.Paid &&  // Ei maksettu.
                                i.Status != InvoiceStatus.Draft)   // Ei luonnos.
                    .ToListAsync();

                // Yhteenvedot maksamattomista laskuista.
                vm.TotalInvoices  = myInvoices.Count;                              // Maksamattomien määrä.
                vm.UnpaidTotal    = myInvoices.Sum(i => i.TotalAmount);            // Summa yhteensä.
                // Count() ilman parametria laskee kaikki — ehto lambdana suodattaa.
                vm.OverdueCount   = myInvoices.Count(i => i.DueDate.Date < today); // Erääntyneet.

                // Pian erääntyvät laskut seuraavalle 7 päivälle.
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

                // ── Uudet lähetetyt laskut (viimeiset 30 päivää) ─────────
                // Asiakaskäyttäjä näkee vastaanotettuja laskuja — "uudet laskut" -widget.
                var since30Days = today.AddDays(-30); // 30 päivää sitten.
                vm.NewSentInvoices = await _db.Invoices
                    .Include(i => i.Lines)
                    .Where(i => i.CustomerId == custId &&
                                i.Status == InvoiceStatus.Sent &&        // Lähetetty, ei luonnos.
                                i.InvoiceDate.Date >= since30Days)        // Viimeisen 30 pv sisällä.
                    .OrderByDescending(i => i.InvoiceDate) // Uusin ensin.
                    .Take(5)
                    .ToListAsync();
            }
        }

        // ── Sparkline-sarjat viimeiseltä 14 päivältä ────────────────────────
        // Sparkline = pieni kaaviotrendiviiva dashboardin KPI-ruuduissa.
        // Luodaan 14 päivän päivämääräsarja indeksoituja arvoja varten.
        var sparkFrom = today.AddDays(-13); // 14 päivää sitten (13 päivää + tänään = 14 päivää).
        // Enumerable.Range(0, 14) = luvut 0..13.
        // Select(i => sparkFrom.AddDays(i)) = muodostaa päivämäärät: eilen-13, ..., eilinen, tänään.
        var days = Enumerable.Range(0, 14).Select(i => sparkFrom.AddDays(i)).ToList();

        // Rajaus: admin näkee kaiken (null = ei asiakasrajausta), asiakas vain omansa.
        int? scopeCustomerId = isAdmin ? null : currentUser?.CustomerId;

        // Haetaan luotujen tikettien päivämäärät 14 päivän ajanjaksolta.
        // Ei haeta kaikkia tikettejä — pelkkä Date-kenttä riittää trendiviivaan.
        var ticketsCreated = await _db.Tickets
            .Where(t => t.CreatedAt >= sparkFrom &&
                        // Null = admin → ei rajausta. Muuten → vain oman asiakkaan tiketit.
                        (scopeCustomerId == null || t.CustomerId == scopeCustomerId))
            .Select(t => t.CreatedAt.Date) // Poimitaan vain päivämäärä (ei kellonaikaa).
            .ToListAsync();
        // Muodostetaan sparkline-taulukko: jokaiselle päivälle, kuinka monta tikettiä luotiin.
        // Count(x => x == d) = kyseisen päivän tikettien määrä.
        // (double) = muunnetaan liukuluvuksi kaaviokirjastoa varten.
        vm.TicketsCreatedSpark = days.Select(d => (double)ticketsCreated.Count(x => x == d)).ToArray();

        // Haetaan lähetettyjen laskujen tiedot sparklineja varten.
        // Ladataan sekä päivämäärä että summa — tarvitaan kahteen eri sparklineen.
        var invoicesIssued = await _db.Invoices
            .Where(i => i.InvoiceDate >= sparkFrom &&
                        i.Status != InvoiceStatus.Draft &&    // Vain lähetetyt, ei luonnokset.
                        (scopeCustomerId == null || i.CustomerId == scopeCustomerId))
            .Select(i => new { i.InvoiceDate, i.TotalAmount }) // Anonyymi objekti — vain tarvittavat kentät.
            .ToListAsync();
        // Laskujen lukumäärä per päivä (trendiviiva).
        vm.InvoicesIssuedSpark = days.Select(d => (double)invoicesIssued.Count(x => x.InvoiceDate.Date == d)).ToArray();
        // Kumulatiivinen maksamaton summa per päivä (kertyvä trendi).
        // Where(x => x.InvoiceDate.Date <= d) = kaikki laskut tähän päivään mennessä.
        // Sum(x => x.TotalAmount) = niiden yhteissumma.
        vm.UnpaidTotalSpark    = days.Select(d => (double)invoicesIssued
                                                          .Where(x => x.InvoiceDate.Date <= d)
                                                          .Sum(x => x.TotalAmount)).ToArray();
        // Avointen tikettien sparkline — yksinkertaistus: sama arvo joka päivälle
        // (absoluuttinen nykytila eikä historiallinen muutos).
        vm.TicketsOpenSpark = days.Select(_ => (double)vm.OpenTickets).ToArray();

        // ── Tulevat varaukset (kalenteriwidget) ──────────────────────────────
        // Näytetään seuraavat 14 päivää — asiakaskalenteri dashboardilla.
        var nowLocal = DateTime.Now;       // Paikallinen aika (varausten vertailuun).
        var in14Days = nowLocal.AddDays(14);
        vm.UpcomingBookingSlots = await _db.BookingSlots
            .Include(s => s.Bookings) // Paikkatilanne — onko vapaata jäljellä.
            .Where(s => s.IsActive &&           // Näkyy asiakkaille.
                        s.StartTime >= nowLocal && // Alkaa tulevaisuudessa.
                        s.StartTime <= in14Days)   // Seuraavan kahden viikon sisällä.
            .OrderBy(s => s.StartTime)  // Aikajärjestyksessä.
            .Take(6)                    // Korkeintaan 6 varausikkunaa kalenteriwidgetille.
            .ToListAsync();

        // Renderöidään dashboard ViewModel:illa.
        return View(vm);
    }

    // ── POST /Home/SaveLayout ────────────────────────────────────────────────
    /// <summary>
    /// Tallentaa käyttäjän mukautetun widget-järjestyksen.
    /// Selain lähettää widget-avaimien taulukon JSON-muodossa (esim. ["tickets", "kpi"]).
    /// Validoidaan että avaimet ovat tunnettuja, jotta tietokantaan ei pääse roskaa.
    /// </summary>
    // [HttpPost] = käsittelee vain POST-pyyntöjä.
    // [Authorize] = kirjautuminen vaaditaan.
    [HttpPost]
    [Authorize]
    // [FromBody] = data tulee HTTP-pyynnön rungosta JSON-muodossa (ei lomakkeena).
    // Tätä kutsuu JavaScript kun käyttäjä raahaa widgetit uuteen järjestykseen.
    public async Task<IActionResult> SaveLayout([FromBody] string[] order)
    {
        var user = await _userManager.GetUserAsync(User);
        // Unauthorized() = HTTP 401 — pitäisi olla mahdotonta koska [Authorize] on päällä.
        if (user is null) return Unauthorized();

        // Sallitut widget-avaimet — whitelist-suodatus.
        // HashSet-haku on O(1), tehokas vaikka listalla olisi paljon avaimia.
        var allowed = new HashSet<string> { "kpi", "tickets", "invoices", "calendar", "quickactions" };
        // Suodatetaan pois tuntemattomat avaimet ja duplikaatit.
        // Distinct() = poistaa toistuvat avaimet (ei voi olla sama widget kahdesti).
        var clean   = order.Where(w => allowed.Contains(w)).Distinct().ToArray();

        // Tallennetaan pilkulla erotettuna merkkijonona tietokantaan.
        // Esim. ["kpi", "tickets"] → "kpi,tickets"
        user.DashboardLayout = string.Join(",", clean);
        // UpdateAsync tallentaa muutokset ASP.NET Identity -tauluun.
        await _userManager.UpdateAsync(user);
        // Ok() = HTTP 200 — JavaScript tietää tallennuksen onnistuneen.
        return Ok();
    }

    // ── GET /Home/Privacy ────────────────────────────────────────────────────
    /// <summary>Staattinen tietosuojaseloste-sivu.</summary>
    // [AllowAnonymous] = sivu on julkinen — kirjautumaton käyttäjä voi lukea.
    [AllowAnonymous]
    public IActionResult Privacy() => View();

    // ── GET /Home/Error ──────────────────────────────────────────────────────
    /// <summary>
    /// Virhesivu. Välitetään pyyntötunnus,
    /// jolla virhe voidaan jäljittää lokeista.
    /// ResponseCache estää välimuistituksen — virhesivua ei saa cacheta.
    /// </summary>
    // ResponseCache(Duration=0, NoStore=true) = HTTP-otsikko "Cache-Control: no-store".
    // Tärkeää: virhesivu näyttää tilakohtaisen tiedon — välimuistoitu virhesivu
    // voisi näyttää väärän käyttäjän virhetiedot.
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        // Activity.Current?.Id = aktiivisen diagnostiikkaoperaation tunnus (ASP.NET Core -putki).
        // ?? = jos null, käytetään HttpContext.TraceIdentifier:ia (pyynnön tunnistemerkkijono).
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
