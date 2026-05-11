// Nimiavaruuksien tuonnit.
using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Asiakasyritysten hallinta. Käyttäjä voi listata, luoda, muokata
/// ja tarkastella asiakkaita. Asiakaskäyttäjillä on yleensä rajatumpi
/// pääsy — useimmat toiminnot edellyttävät admin-roolia.
/// </summary>
// [Authorize] = koko controller vaatii kirjautumisen.
// Kirjautumattomat käyttäjät ohjataan automaattisesti kirjautumissivulle.
[Authorize]
public class CustomerController : Controller
{
    // Tietokantayhteys — injektoitu konstruktorissa.
    private readonly ApplicationDbContext _db;
    // Diagnostiikkaloki kehittäjälle.
    private readonly ILogger<CustomerController> _logger;

    // Konstruktori: DI-säiliö täyttää parametrit automaattisesti.
    public CustomerController(ApplicationDbContext db, ILogger<CustomerController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── GET /Customer ─────────────────────────────────────────────────────────
    // Listaa kaikki asiakkaat. Hakutoiminto suodattaa listaa.
    public async Task<IActionResult> Index(string? search)
    {
        // AsQueryable() palauttaa kyselyobjektin jolle voidaan lisätä ehtoja.
        // Kyselyä ei lähetetä tietokantaan ennen kuin kutsutaan ToListAsync().
        var query = _db.Customers.AsQueryable();

        // Jos hakusana on annettu, suodatetaan tuloksia.
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Poistetaan ylimääräiset välilyönnit hakusanan alusta/lopusta.
            search = search.Trim();
            // LINQ:n Where() lisää ehtoja SQL:n WHERE-lausekkeen tapaan.
            // Contains() = LIKE '%hakusana%' SQL:ssa.
            // Hakee kolmesta kentästä: nimi, Y-tunnus ja sähköposti.
            query = query.Where(c =>
                c.CompanyName.Contains(search) ||
                c.BusinessId.Contains(search) ||
                c.ContactEmail.Contains(search));
        }

        // OrderBy() lajittelee aakkosjärjestykseen.
        // ToListAsync() lähettää SQL-kyselyn tietokantaan ja odottaa vastausta.
        var customers = await query
            .OrderBy(c => c.CompanyName)
            .ToListAsync();

        // ViewBag = dynaaminen sanakirja tiedoille, joita ei voi laittaa ViewModeliin.
        // Tässä tallennetaan hakusana takaisin näkymälle (jotta hakukenttä näyttää saman arvon).
        ViewBag.Search = search;
        // Lähetetään lista näkymälle renderöitäväksi.
        return View(customers);
    }

    // ── GET /Customer/Details/5 ───────────────────────────────────────────────
    // Näyttää yhden asiakkaan tiedot: viimeisimmät tiketit, laskut, sopimukset ja käyttäjät.
    public async Task<IActionResult> Details(int id)
    {
        // Haetaan asiakas ID:n perusteella.
        // Include() = JOIN-kysely: haetaan myös liittyvät taulut yhdellä SQL-kyselyllä.
        // "OrderByDescending...Take(10)" = lataa vain viimeisimmät 10 tikettiä/laskua,
        // ei kaikkia — suorituskykyoptimoint.
        var customer = await _db.Customers
            .Include(c => c.Tickets.OrderByDescending(t => t.CreatedAt).Take(10))
            .Include(c => c.Invoices.OrderByDescending(i => i.InvoiceDate).Take(10))
            .Include(c => c.Contracts)
            .Include(c => c.Users)
            .FirstOrDefaultAsync(c => c.Id == id);

        // Jos asiakasta ei löydy, palautetaan 404 Not Found -vastaus.
        if (customer is null)
            return NotFound();

        // Lähetetään asiakkaan tiedot näkymälle.
        return View(customer);
    }

    // ── GET /Customer/Create ──────────────────────────────────────────────────
    // Näyttää tyhjän asiakkaan luontilomakkeen.
    // [Authorize(Roles = "Admin")] = vain admin saa luoda asiakkaita.
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        // Tyhjä ViewModel oletusarvoilla (ei tarvita tietokantakutsuja).
        return View(new CustomerFormViewModel());
    }

    // ── POST /Customer/Create ─────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model)
    {
        // Validoidaan lomake.
        if (!ModelState.IsValid)
            return View(model);

        // Tarkistetaan Y-tunnuksen uniikkius.
        // AnyAsync() = SQL:n EXISTS-kysely — palauttaa true jos vastaava rivi löytyy.
        if (await _db.Customers.AnyAsync(c => c.BusinessId == model.BusinessId))
        {
            // Lisätään virheilmoitus tiettyyn kenttään.
            ModelState.AddModelError(nameof(model.BusinessId),
                "Y-tunnus on jo käytössä toisella asiakkaalla.");
            return View(model);
        }

        // Luodaan domain-entiteetti ViewModelista.
        // Tämä muunnos on tärkeä: ViewModel voi sisältää lomakkeen kenttiä,
        // joita ei tallenneta tietokantaan (esim. pudotusvalikot).
        var customer = new Customer
        {
            CompanyName = model.CompanyName,
            BusinessId = model.BusinessId,
            ContactEmail = model.ContactEmail,
            Phone = model.Phone,
            Address = model.Address,
            IsActive = model.IsActive,
            // CreatedAt asetetaan manuaalisesti UTC:nä.
            CreatedAt = DateTime.UtcNow
        };

        // Add() merkitsee entiteetin "lisättäväksi" EF Corelle.
        _db.Customers.Add(customer);
        // SaveChangesAsync() generoi ja suorittaa INSERT SQL:n.
        // Tämän jälkeen customer.Id on täytetty automaattisesti.
        await _db.SaveChangesAsync();

        _logger.LogInformation("Asiakas {Name} luotu (Id={Id}).", customer.CompanyName, customer.Id);
        TempData["SuccessMessage"] = $"Asiakas \"{customer.CompanyName}\" luotu onnistuneesti.";
        // Ohjataan asiakaslistaan.
        return RedirectToAction(nameof(Index));
    }

    // ── GET /Customer/Edit/5 ──────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        // FindAsync() = haku pääavaimella — nopein tapa hakea yksittäinen rivi.
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null)
            return NotFound();

        // Täytetään ViewModel olemassaolevasta entiteetistä muokkaamista varten.
        return View(new CustomerFormViewModel
        {
            Id = customer.Id,           // Tarvitaan muokkauksen tunnistamiseen POST:ssa.
            CompanyName = customer.CompanyName,
            BusinessId = customer.BusinessId,
            ContactEmail = customer.ContactEmail,
            Phone = customer.Phone,
            Address = customer.Address,
            IsActive = customer.IsActive
        });
    }

    // ── POST /Customer/Edit/5 ─────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel model)
    {
        // Varmistetaan että URL:n id täsmää lomakkeen Id-kenttään.
        // Estää IDOR-hyökkäyksen (Insecure Direct Object Reference):
        // käyttäjä ei voi vaihtaa lomakkeen Id:tä muokatakseen eri asiakkaan tietoja.
        if (id != model.Id)
            return BadRequest();

        if (!ModelState.IsValid)
            return View(model);

        // Tarkistetaan Y-tunnuksen uniikkius, mutta poislukien itsensä (c.Id != id).
        // Ilman "Id != id" -ehtoa muokkaaminen hylkäisi oman Y-tunnuksensa.
        if (await _db.Customers.AnyAsync(c => c.BusinessId == model.BusinessId && c.Id != id))
        {
            ModelState.AddModelError(nameof(model.BusinessId),
                "Y-tunnus on jo käytössä toisella asiakkaalla.");
            return View(model);
        }

        // Haetaan alkuperäinen entiteetti tietokannasta muokattavaksi.
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null)
            return NotFound();

        // Päivitetään kenttät ViewModelista entiteettiin.
        // EF Core seuraa muutoksia automaattisesti — SaveChangesAsync() generoi UPDATE SQL:n.
        customer.CompanyName = model.CompanyName;
        customer.BusinessId = model.BusinessId;
        customer.ContactEmail = model.ContactEmail;
        customer.Phone = model.Phone;
        customer.Address = model.Address;
        customer.IsActive = model.IsActive;

        // UPDATE SQL lähetetään tietokantaan.
        await _db.SaveChangesAsync();

        _logger.LogInformation("Asiakas {Name} päivitetty (Id={Id}).", customer.CompanyName, customer.Id);
        TempData["SuccessMessage"] = $"Asiakkaan \"{customer.CompanyName}\" tiedot päivitetty.";
        // Ohjataan takaisin asiakkaan tietosivulle.
        return RedirectToAction(nameof(Details), new { id = customer.Id });
    }

    // ── POST /Customer/ToggleActive/5 ─────────────────────────────────────────
    // Vaihtaa asiakkaan aktiivisuuden (aktivoi tai deaktivoi).
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();

        // "! " = looginen negaatio. Jos true → false, jos false → true.
        // Yksi rivi hoitaa sekä aktivoinnin että deaktivoinnin.
        customer.IsActive = !customer.IsActive;
        await _db.SaveChangesAsync();

        // Lokiviesti muuttuu tilanteen mukaan.
        string status = customer.IsActive ? "aktivoitu" : "deaktivoitu";
        _logger.LogInformation("Asiakas {Name} {Status} (Id={Id}).", customer.CompanyName, status, id);
        TempData["SuccessMessage"] = $"Asiakas \"{customer.CompanyName}\" {status}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── POST /Customer/Delete/5 ───────────────────────────────────────────────
    // Poistaa asiakkaan kokonaan — vain jos sillä ei ole tikettejä/laskuja/sopimuksia.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        // Haetaan asiakas Include-metodilla jotta liittyvät taulut ladataan.
        // Tarvitaan tarkistukseen: onko asiakkaalla dataa, joka estää poistamisen?
        var customer = await _db.Customers
            .Include(c => c.Tickets)
            .Include(c => c.Invoices)
            .Include(c => c.Contracts)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer is null)
            return NotFound();

        // Turvallisuustarkistus: ei poisteta asiakasta jos sillä on historiaa.
        // Syy: poistaminen rikkoisi viittaukset (tiketeissä on CustomerId, joka viittaisi poistettuun).
        // Parempi vaihtoehto: ToggleActive → asiakas piilotetaan mutta tiedot säilyvät.
        if (customer.Tickets.Any() || customer.Invoices.Any() || customer.Contracts.Any())
        {
            TempData["ErrorMessage"] =
                "Asiakasta ei voi poistaa, koska sillä on avoimia tikettejä, laskuja tai sopimuksia. " +
                "Aseta asiakas epäaktiiviseksi sen sijaan.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Remove() merkitsee entiteetin poistettavaksi.
        _db.Customers.Remove(customer);
        // DELETE SQL lähetetään tietokantaan.
        await _db.SaveChangesAsync();

        _logger.LogInformation("Asiakas {Name} poistettu (Id={Id}).", customer.CompanyName, id);
        TempData["SuccessMessage"] = $"Asiakas \"{customer.CompanyName}\" poistettu.";
        // Ohjataan asiakaslistaan (poistettu asiakas ei enää ole olemassa).
        return RedirectToAction(nameof(Index));
    }
}
