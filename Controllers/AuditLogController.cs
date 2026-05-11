// Nimiavaruuksien tuonnit.
using HakaTech.Portal.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Audit-lokin selailusivu — vain ylläpitäjille. Lokimerkintöjen avulla
/// voi tutkia kuka teki mitä, milloin ja mistä IP:stä (esim. tietoturvatutkinta).
/// </summary>
// Audit-loki on vain luettavissa — ei muokkaus- tai poistotoimintoja.
// Koko controller on Admin-roolille rajoitettu.
[Authorize(Roles = "Admin")]
public class AuditLogController : Controller
{
    // Tietokantayhteys — injektoitu konstruktorissa.
    private readonly ApplicationDbContext _db;

    // Konstruktori: DI-säiliö täyttää parametrin.
    public AuditLogController(ApplicationDbContext db)
    {
        _db = db;
    }

    // ── GET /AuditLog ─────────────────────────────────────────────────────────
    // Listaa lokimerkinnät sivutettuna, suodatusmahdollisuuksilla.
    public async Task<IActionResult> Index(
        string? action,       // Toimintosuodatin (esim. "Login", "TicketCreated").
        string? userEmail,    // Käyttäjäsuodatin.
        string? entityType,   // Kohdetyyppisuodatin (esim. "Ticket", "Invoice").
        DateTime? from,       // Aikarajasuodatin: alku.
        DateTime? to,         // Aikarajasuodatin: loppu.
        int page = 1)         // Sivunumero — oletuksena ensimmäinen sivu.
    {
        // Sivun koko: 50 merkintää per sivu.
        // "const" = vakio, jonka arvo ei muutu ohjelman ajon aikana.
        const int pageSize = 50;

        // Kyselyobjekti jolle lisätään ehtoja alla.
        var query = _db.AuditLogs.AsQueryable();

        // Jokainen suodatin lisätään kyselyyn vain jos sen arvo on annettu.
        // Näin käyttäjä voi suodattaa yhdellä tai useammalla kriteerillä.

        // Toimintosuodatin — etsii osittaista vastaavuutta (Contains = LIKE '%...%').
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action.Contains(action));

        // Sähköpostisuodatin — tarkistaa myös ettei ole null (nullable-kenttä).
        if (!string.IsNullOrWhiteSpace(userEmail))
            query = query.Where(a => a.UserEmail != null && a.UserEmail.Contains(userEmail));

        // Kohdetyyppisuodatin — tarkka haku (== eikä Contains).
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        // Aikarajasuodattimet — >= alku ja < loppu+1 (koko loppupäivä mukaan).
        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from.Value);

        // AddDays(1) = loppupäivä otetaan kokonaan mukaan (muuten loppuu kesken päivää).
        if (to.HasValue)
            query = query.Where(a => a.Timestamp < to.Value.AddDays(1));

        // Lasketaan kaikkien suodatettujen merkintöjen kokonaismäärä (sivutusta varten).
        int total = await query.CountAsync();

        // Haetaan yksi sivu tuloksia.
        var logs = await query
            .OrderByDescending(a => a.Timestamp)     // Uusin ensin.
            .Skip((page - 1) * pageSize)             // Ohitetaan edelliset sivut.
            .Take(pageSize)                           // Otetaan tasan pageSize merkintää.
            .ToListAsync();

        // Sivutustiedot näkymälle.
        ViewBag.Page       = page;
        ViewBag.PageSize   = pageSize;
        ViewBag.Total      = total;
        // Math.Ceiling pyöristää ylöspäin: 51 merkintää / 50 per sivu = 2 sivua.
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

        // Suodatusarvojen palauttaminen näkymälle (jotta lomakkeet pysyvät täytettyinä).
        ViewBag.FilterAction     = action;
        ViewBag.FilterUserEmail  = userEmail;
        ViewBag.FilterEntityType = entityType;
        // Muotoillaan päivämäärät "yyyy-MM-dd"-muotoon HTML date-kenttiä varten.
        ViewBag.FilterFrom       = from?.ToString("yyyy-MM-dd");
        ViewBag.FilterTo         = to?.ToString("yyyy-MM-dd");

        // Haetaan kaikki eri toimintotyypit pudotusvalikon täyttämistä varten.
        // Select(a => a.Action) = SQL:n SELECT DISTINCT Action.
        // Distinct() = ei duplikaatteja.
        ViewBag.ActionTypes = await _db.AuditLogs
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        return View(logs);
    }
}
