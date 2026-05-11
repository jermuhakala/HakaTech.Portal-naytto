// Nimiavaruuksien tuonnit.
using HakaTech.Portal.Data;                  // ApplicationDbContext — tietokantayhteys.
using HakaTech.Portal.Models.Domain;         // ApplicationUser — kirjautunut käyttäjä.
using Microsoft.AspNetCore.Authorization;    // [Authorize]-attribuutti.
using Microsoft.AspNetCore.Identity;         // UserManager.
using Microsoft.AspNetCore.Mvc;              // ControllerBase, ActionResult, Ok()...
using Microsoft.EntityFrameworkCore;         // ToListAsync, AsNoTracking, EF.Functions.Like...

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Globaali pikahaku (cmdk-style). API-controller, jota cmdk-käyttöliittymä
/// kutsuu AJAX:lla — palauttaa JSON:n. Hakee tikettejä, asiakkaita,
/// laskuja ja tietopankin artikkeleita yhdellä kyselyllä.
/// Asiakkaalle näytetään vain oman yrityksen tulokset.
/// </summary>
// [ApiController] = erikoistaa controllerin REST API -käyttöön:
//   - Automaattinen HTTP 400 palautus jos [FromBody]-validointi epäonnistuu.
//   - [FromQuery]/[FromBody]-attribuutit lisätään automaattisesti parametreihin.
//   - Vastaus muotoillaan JSON:na (ei HTML-näkymää).
[ApiController]
// [Authorize] = kirjautuminen vaaditaan — hakua ei voi tehdä kirjautumatta.
[Authorize]
// [Route("api/search")] = URL-reitti tälle controllerille.
// Tämä controller vastaa pyyntöihin GET /api/search?q=xxx.
// "api/"-etuliite erottaa JSON-rajapinnan tavallisista HTML-sivuista.
[Route("api/search")]
// "sealed" = tätä luokkaa ei voi periyttää (tehokkuus ja selkeys).
// ControllerBase = kevyt API-controller ilman näkymätukea (Controller-luokka on täysiversio).
public sealed class SearchController : ControllerBase
{
    // Tietokantayhteys.
    private readonly ApplicationDbContext         _db;
    // UserManager — haetaan kirjautunut käyttäjä ja CustomerId (asiakasrajaus).
    private readonly UserManager<ApplicationUser> _users;

    // Konstruktori: DI-säiliö täyttää parametrit.
    public SearchController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db    = db;
        _users = users;
    }

    // ── Sisäiset tietorakenteet (DTO = Data Transfer Object) ────────────────
    // Nämä luokat ovat tämän controllerin sisäisiä — niitä ei käytetä muualla.
    // "sealed" = ei voida periyttää.

    /// <summary>Yksi hakutulos (tiketit, laskut, asiakkaat ja KB-artikkelit).</summary>
    // "init" = voidaan asettaa vain olion luomishetkellä (immutable after init).
    public sealed class SearchHit
    {
        public int     Id       { get; init; }               // Kohteen tietokantatunnus.
        public string  Title    { get; init; } = string.Empty; // Hakutuloksen otsikko.
        public string? Subtitle { get; init; }               // Toissijainen teksti (esim. yrityksen nimi). null = ei näytetä.
        public string  Url      { get; init; } = string.Empty; // Linkki kohteen tietosivulle.
        public string? Status   { get; init; }               // Tila (esim. "Open", "Paid"). null = ei tähän tyyppiin.
    }

    /// <summary>Kaikki hakutulokset yhtenä JSON-vastauksena.</summary>
    // Näkymä näyttää nämä neljä listaa erillisinä osioina (tiketit, laskut, asiakkaat, tietopankki).
    public sealed class SearchResultsDto
    {
        public List<SearchHit> Tickets    { get; set; } = new(); // Tiketit.
        public List<SearchHit> Invoices   { get; set; } = new(); // Laskut.
        public List<SearchHit> Customers  { get; set; } = new(); // Asiakkaat (vain admin).
        public List<SearchHit> KbArticles { get; set; } = new(); // Tietopankin artikkelit.
    }

    // ── GET /api/search?q=xxx&take=5 ────────────────────────────────────────
    // Globaali hakupääte — palauttaa JSON-vastauksen.
    // ActionResult<T> = ASP.NET Core voi automaattisesti serialisoida T JSON:ksi.
    [HttpGet]
    public async Task<ActionResult<SearchResultsDto>> Get(
        [FromQuery] string? q,          // Hakusana URL-parametrina. null = ei haeta.
        [FromQuery] int take = 5)       // Tulosten enimmäismäärä per osio. Oletuksena 5.
    {
        // Aloitetaan tyhjällä tuloksella — palautetaan se heti jos hakusana puuttuu.
        var result = new SearchResultsDto();
        // Alle 2 merkin hakusanalla ei tehdä tietokantakutsua — liian laaja haku.
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return Ok(result);

        // Math.Clamp() rajoittaa tulosmäärän välille [1, 10] — ei liikaa eikä liian vähän.
        take = Math.Clamp(take, 1, 10);

        // Haetaan kirjautunut käyttäjä asiakasrajauksen vuoksi.
        var me     = await _users.GetUserAsync(User);
        var isAdmin = User.IsInRole("Admin");
        // null = admin (ei rajausta), muuten käyttäjän CustomerId.
        int? scopeCustomerId = isAdmin ? null : me?.CustomerId;
        // Trim() poistaa ylimääräiset välilyönnit hakusanan ympäriltä.
        var term = q.Trim();

        // ── Tiketit ──────────────────────────────────────────────────────────
        // AsNoTracking() = EF Core ei seuraa muutoksia (read-only) → nopeampi haku.
        var ticketsQ = _db.Tickets.AsNoTracking()
            .Include(t => t.Customer) // Yrityksen nimi hakutuloksen alaotsikkoon.
            // Asiakasrajaus: null = admin näkee kaikki, muuten vain oman yrityksen.
            .Where(t => scopeCustomerId == null || t.CustomerId == scopeCustomerId)
            // EF.Functions.Like() = SQL LIKE-operaattori, nopeampi kuin .Contains() tietyissä tilanteissa.
            // $"%{term}%" = hakusana voi olla missä kohtaa merkkijonoa tahansa.
            .Where(t => EF.Functions.Like(t.Title, $"%{term}%")
                     || EF.Functions.Like(t.Description ?? "", $"%{term}%"));
        result.Tickets = await ticketsQ
            .OrderByDescending(t => t.CreatedAt) // Uusin ensin.
            .Take(take)
            .Select(t => new SearchHit
            {
                Id       = t.Id,
                Title    = t.Title,
                // Ternary: yrityksen nimi alaotsikkona, tai null jos ei asiakasta.
                Subtitle = t.Customer != null ? t.Customer.CompanyName : null,
                // Suora URL-linkki tiketin tietosivulle.
                Url      = $"/Ticket/Details/{t.Id}",
                // .ToString() muuntaa enum-arvon tekstiksi (esim. TicketStatus.Open → "Open").
                Status   = t.Status.ToString()
            })
            .ToListAsync();

        // ── Laskut ───────────────────────────────────────────────────────────
        var invoicesQ = _db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => scopeCustomerId == null || i.CustomerId == scopeCustomerId)
            // Laskuhaku: vain laskunumerolla (INV-2025-001 tms.) — ei laskurivien kuvauksilla.
            .Where(i => EF.Functions.Like(i.InvoiceNumber, $"%{term}%"));
        result.Invoices = await invoicesQ
            .OrderByDescending(i => i.InvoiceDate)
            .Take(take)
            .Select(i => new SearchHit
            {
                Id       = i.Id,
                Title    = i.InvoiceNumber,    // Laskunumero hakutuloksen otsikkona.
                Subtitle = i.Customer != null ? i.Customer.CompanyName : null,
                Url      = $"/Invoice/Details/{i.Id}",
                Status   = i.Status.ToString()  // Esim. "Draft", "Sent", "Paid".
            })
            .ToListAsync();

        // ── Asiakkaat (vain admin) ────────────────────────────────────────────
        // Asiakashaku on salattu asiakkailta — asiakas ei saa selata muiden yritysten tietoja.
        if (isAdmin)
        {
            result.Customers = await _db.Customers.AsNoTracking()
                // Haetaan yrityksen nimellä tai Y-tunnuksella.
                .Where(c => EF.Functions.Like(c.CompanyName, $"%{term}%")
                         || EF.Functions.Like(c.BusinessId ?? "", $"%{term}%"))
                .OrderBy(c => c.CompanyName) // Aakkosjärjestys.
                .Take(take)
                .Select(c => new SearchHit
                {
                    Id       = c.Id,
                    Title    = c.CompanyName,
                    Subtitle = c.BusinessId,   // Y-tunnus alaotsikkona.
                    Url      = $"/Customer/Details/{c.Id}",
                    Status   = c.IsActive ? "Active" : "Inactive" // Onko asiakas aktiivinen.
                })
                .ToListAsync();
        }

        // ── Tietopankin artikkelit ────────────────────────────────────────────
        // Julkaistut artikkelit — kaikille kirjautuneille käyttäjille (ei asiakasrajausta).
        result.KbArticles = await _db.KnowledgeBaseArticles.AsNoTracking()
            .Where(a => a.IsPublished) // Vain julkaistut artikkelit.
            .Where(a => EF.Functions.Like(a.Title, $"%{term}%")
                     || EF.Functions.Like(a.Content ?? "", $"%{term}%"))
            .OrderByDescending(a => a.UpdatedAt) // Uusin päivitys ensin.
            .Take(take)
            .Select(a => new SearchHit
            {
                Id       = a.Id,
                Title    = a.Title,
                Subtitle = null, // Artikkeleilla ei alaotsikkoa (kategoria puuttuu tässä hausta).
                Url      = $"/KnowledgeBase/Details/{a.Id}"
                // Status puuttuu — artikkeleilla ei tilakenttää.
            })
            .ToListAsync();

        // Ok() = HTTP 200 + JSON-vastaus automaattisella serialisoinnilla.
        return Ok(result);
    }
}
