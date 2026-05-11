// Nimiavaruuksien tuonnit.
using HakaTech.Portal.Data;                        // ApplicationDbContext — tietokantayhteys.
using HakaTech.Portal.Models.Domain;               // Invoice, InvoiceLine, InvoiceAttachment, InvoiceStatus...
using HakaTech.Portal.Models.ViewModels;           // InvoiceCreateViewModel, InvoiceStatusViewModel...
using HakaTech.Portal.Services;                    // IFileStorageService, IEmailService, IAuditService.
using Microsoft.AspNetCore.Authorization;          // [Authorize]-attribuutti.
using Microsoft.AspNetCore.Identity;               // UserManager.
using Microsoft.AspNetCore.Mvc;                    // Controller, IActionResult, TempData...
using Microsoft.AspNetCore.Mvc.Rendering;          // SelectListItem — pudotusvalikon vaihtoehto.
using Microsoft.AspNetCore.StaticFiles;            // FileExtensionContentTypeProvider — MIME-tyyppi tiedostopäätteestä.
using Microsoft.EntityFrameworkCore;               // Include, ToListAsync, AnyAsync...

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Laskujen controller. Vastaa laskujen luomisesta (admin), näyttämisestä
/// (asiakkaalle vain omat), PDF-vientinä ja tilanvaihdosta (Sent/Paid jne.).
/// Asiakas voi myös ladata laskuun liitettyjä tiedostoja.
/// </summary>
// [Authorize] = kirjautuminen vaaditaan kaikissa action-metodeissa.
// Admin ja Customer voivat molemmat käyttää controlleria, mutta eri oikeuksilla.
[Authorize]
public class InvoiceController : Controller
{
    // Tietokantayhteys.
    private readonly ApplicationDbContext         _db;
    // UserManager — haetaan kirjautunut käyttäjä ja tarkistetaan CustomerId.
    private readonly UserManager<ApplicationUser> _userManager;
    // Diagnostiikkaloki kehittäjälle.
    private readonly ILogger<InvoiceController>   _logger;
    // Tiedostopalvelu — liitteiden tallentaminen levylle ja polkujen hallinta.
    private readonly IFileStorageService           _fileStorage;
    // Web-hostin ympäristö — tiedostopolkujen rakentamista varten.
    private readonly IWebHostEnvironment           _env;
    // Sähköpostipalvelu — ilmoitukset asiakkaalle uusista laskuista.
    private readonly IEmailService                 _emailService;
    // Auditointipalvelu — laskun lataukset ja tilamuutokset kirjataan lokiin.
    private readonly IAuditService                 _audit;

    // Konstruktori: DI-säiliö täyttää kaikki parametrit.
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

    // ── GET /Invoice ─────────────────────────────────────────────────────────
    // Laskujen lista. Admin näkee kaikki, asiakas vain omansa.
    // Suodattimet: asiakas ja tila.
    public async Task<IActionResult> Index(
        int? customerId,        // Asiakassuodatin (vain admin voi käyttää). null = kaikki.
        InvoiceStatus? status)  // Tilasuodatin. null = kaikki tilat.
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        // Aloitetaan kyselyobjektilla — lisätään ehtoja alla roolin mukaan.
        // Include() = JOIN: ladataan asiakkaan nimi ja laskurivit (tarvitaan TotalAmount-laskentaan).
        var query = _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Lines) // Rivit tarvitaan TotalAmount-laskennan laukaukseen.
            .AsQueryable();

        // Asiakaskäyttäjä näkee vain oman yrityksensä laskut.
        if (!isAdmin)
        {
            // Jos käyttäjällä ei ole CustomerId:tä, palautetaan tyhjä lista (ei virhettä).
            // Enumerable.Empty<Invoice>() = tyhjä kokoelma ilman tietokantakutsua.
            if (currentUser?.CustomerId is null)
                return View(Enumerable.Empty<Invoice>());

            // Rajoitetaan kysely käyttäjän oman yrityksen laskuihin.
            query = query.Where(i => i.CustomerId == currentUser.CustomerId);
        }
        // Admin voi suodattaa tietyn asiakkaan laskut (pudotusvalikosta).
        else if (customerId.HasValue)
        {
            query = query.Where(i => i.CustomerId == customerId.Value);
        }

        // Tilasuodatin — lisätään kyselyyn vain jos parametri on annettu.
        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        // Haetaan laskut uusimmasta vanhimpaan.
        var invoices = await query
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();

        // Välitetään suodattimien tila näkymälle (lomake pysyy täytettynä).
        ViewBag.StatusFilter   = status;
        ViewBag.CustomerFilter = customerId;
        // Näkymä muuttuu roolin mukaan (esim. admin näkee "Asiakas"-sarakkeen).
        ViewBag.IsAdmin        = isAdmin;
        return View(invoices);
    }

    // ── GET /Invoice/Details/5 ───────────────────────────────────────────────
    // Yksittäisen laskun tiedot, rivit, liitteet ja tila.
    public async Task<IActionResult> Details(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        // Haetaan lasku kaikilla tarvittavilla suhteilla.
        var invoice = await _db.Invoices
            .Include(i => i.Customer)              // Asiakkaan nimi otsikkoon.
            .Include(i => i.Lines)                 // Laskurivit hintoineen.
            // Liitteet aikajärjestyksessä — ladataan myös lataajan tiedot.
            .Include(i => i.Attachments.OrderBy(a => a.UploadedAt))
                .ThenInclude(a => a.UploadedByUser) // Kuka latasi liitteen.
            .FirstOrDefaultAsync(i => i.Id == id);

        // Jos laskua ei löydy, palautetaan 404.
        if (invoice is null)
            return NotFound();

        // Turvallisuustarkistus: asiakas saa nähdä vain oman yrityksensä laskut.
        // IDOR-suoja (Insecure Direct Object Reference): ilman tätä asiakas voisi
        // vaihtaa URL:n id:tä ja nähdä toisen yrityksen laskun.
        if (!isAdmin && invoice.CustomerId != currentUser?.CustomerId)
            return Forbid(); // HTTP 403 — sinulla ei ole oikeutta tähän laskuun.

        // Välitetään roolitieto näkymälle (admin näkee enemmän toimintoja).
        ViewBag.IsAdmin = isAdmin;

        // Asiakkaan aiemmat laskut — "asiakkaan historia" -osio laskusivulla.
        // Näytetään saman asiakkaan 5 viimeisintä muuta laskua.
        var history = await _db.Invoices
            .Where(i => i.CustomerId == invoice.CustomerId && i.Id != invoice.Id) // Ei itse itseään.
            .OrderByDescending(i => i.InvoiceDate)
            .Take(5)
            .ToListAsync();
        ViewBag.CustomerHistory = history;

        // Tilanmuutoslomakkeen tiedot — admin voi muuttaa laskun tilaa suoraan tältä sivulta.
        ViewBag.StatusModel = new InvoiceStatusViewModel
        {
            Id     = invoice.Id,
            Status = invoice.Status,
            PaidAt = invoice.PaidAt // Maksettu-päivämäärä (null jos ei maksettu).
        };

        return View(invoice);
    }

    // ── GET /Invoice/DownloadPdf/5 ───────────────────────────────────────────
    // Generoi ja palauttaa laskun PDF-muodossa ladattavana tiedostona.
    [HttpGet]
    public async Task<IActionResult> DownloadPdf(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        // Haetaan lasku asiakkaan ja rivien tiedoilla PDF:n sisältöä varten.
        var invoice = await _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice is null) return NotFound();

        // IDOR-suoja: asiakas saa ladata vain oman yrityksensä laskun.
        if (!isAdmin && invoice.CustomerId != currentUser?.CustomerId)
            return Forbid();

        // Luodaan PDF-dokumentti QuestPDF-kirjastolla.
        // InvoicePdfDocument on erillinen luokka joka määrittelee laskun ulkoasun.
        var document = new HakaTech.Portal.Services.InvoicePdfDocument(invoice);
        // GeneratePdf() tuottaa PDF:n tavutaulukkona muistissa (ei kirjoiteta levylle).
        var pdfBytes = QuestPDF.Fluent.GenerateExtensions.GeneratePdf(document);

        // Kirjataan PDF-lataus auditlokiin.
        await _audit.LogAsync("InvoiceDownloaded", "Invoice", id.ToString(),
            $"Lasku {invoice.InvoiceNumber} / {invoice.Customer?.CompanyName}");

        // File() = palauttaa tiedoston HTTP-vastauksena.
        // "application/pdf" = MIME-tyyppi, joka kertoo selaimelle tiedostomuodon.
        // Kolmas parametri = ehdotettu tiedostonimi ladattaessa.
        return File(pdfBytes, "application/pdf", $"Lasku_{invoice.InvoiceNumber}.pdf");
    }

    // ── GET /Invoice/Create ──────────────────────────────────────────────────
    // Näyttää uuden laskun luontilomakkeen — vain adminille.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        int? customerId) // Valinnainen esivalinta: mistä asiakassivulta tultiin.
    {
        var model = new InvoiceCreateViewModel
        {
            // Jos tultiin asiakassivulta, esivalitaan asiakas pudotusvalikosta.
            // "??" = jos customerId on null, käytetään 0 (ei valintaa).
            CustomerId      = customerId ?? 0,
            // Rakennetaan pudotusvalikko aktiivisista asiakkaista.
            CustomerOptions = await BuildCustomerOptions(),
            // Ehdotetaan seuraavaa laskunumeroa formaatissa INV-YYYY-NNN.
            InvoiceNumber   = await SuggestInvoiceNumber()
        };
        return View(model);
    }

    // ── POST /Invoice/Create ─────────────────────────────────────────────────
    // Tallentaa uuden laskun tietokantaan ja lähettää sähköpostin asiakkaalle.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceCreateViewModel model)
    {
        // Tarkistetaan että laskulle on vähintään yksi täytetty rivi.
        // All(l => string.IsNullOrWhiteSpace(l.Description)) = kaikki rivit ovat tyhjiä.
        if (model.Lines is null || model.Lines.Count == 0 ||
            model.Lines.All(l => string.IsNullOrWhiteSpace(l.Description)))
        {
            // string.Empty = virheen avain on tyhjä = virheilmoitus on yleinen (ei kenttäkohtainen).
            ModelState.AddModelError(string.Empty,
                "Laskulle on lisättävä vähintään yksi rivi.");
        }

        // Tarkistetaan laskunumeron uniikkius.
        // AnyAsync() = SQL EXISTS — tarkistaa nopeasti onko sama numero jo käytössä.
        if (await _db.Invoices.AnyAsync(i => i.InvoiceNumber == model.InvoiceNumber))
        {
            ModelState.AddModelError(nameof(model.InvoiceNumber),
                "Laskunumero on jo käytössä.");
        }

        // Jos validointi epäonnistui, palautetaan lomake virheineen.
        if (!ModelState.IsValid)
        {
            // Rakennetaan pudotusvalikko uudelleen — se ei säily lomakkeen palautuksessa.
            model.CustomerOptions = await BuildCustomerOptions();
            return View(model);
        }

        // Luodaan Invoice-entiteetti ViewModelista.
        var invoice = new Invoice
        {
            InvoiceNumber = model.InvoiceNumber,
            CustomerId    = model.CustomerId,
            InvoiceDate   = model.InvoiceDate,
            DueDate       = model.DueDate,
            VatRate       = model.VatRate,   // ALV-prosentti (esim. 0.255 = 25,5 %).
            Notes         = model.Notes,
            Status        = InvoiceStatus.Draft, // Uusi lasku on aina luonnos — admin lähettää erikseen.
            // Muunnetaan laskurivit ViewModelista entiteeteiksi.
            // "!" = null-forgiving: Lines ei voi olla null tässä (tarkistettiin yllä).
            // Where() suodattaa pois tyhjät rivit (JS voi lähettää tyhjiä rivejä).
            Lines         = model.Lines!
                .Where(l => !string.IsNullOrWhiteSpace(l.Description))
                .Select(l => new InvoiceLine
                {
                    Description = l.Description,
                    Quantity    = l.Quantity,
                    UnitPrice   = l.UnitPrice
                }).ToList()
        };

        // Tallennetaan lasku ja sen rivit tietokantaan.
        // EF Core tallentaa molemmat automaattisesti (cascade insert).
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(); // INSERT SQL laskuille ja riveille.

        _logger.LogInformation("Lasku {Num} luotu (Id={Id}).", invoice.InvoiceNumber, invoice.Id);

        // ── Sähköposti-ilmoitus asiakkaalle ─────────────────────────────────
        // Haetaan asiakkaan yhteystiedot sähköpostia varten.
        var customer = await _db.Customers.FindAsync(invoice.CustomerId);
        // Lähetetään vain jos asiakkaalla on sähköpostiosoite.
        if (customer is not null && !string.IsNullOrWhiteSpace(customer.ContactEmail))
        {
            try
            {
                // Rakennetaan HTML-sähköpostirunko raw string literalilla ($""" ... """).
                // {invoice.TotalAmount:N2} = luku desimaaliforrmaatissa, esim. "1 234,56".
                var html = $"""
                    <div style="font-family:Inter,Arial,sans-serif;max-width:600px;margin:0 auto;color:#1e293b">
                      <div style="background:#2563eb;padding:24px 32px;border-radius:8px 8px 0 0">
                        <h1 style="color:#fff;margin:0;font-size:22px">HakaTech – Uusi lasku</h1>
                      </div>
                      <div style="background:#f8fafc;padding:24px 32px;border-radius:0 0 8px 8px;border:1px solid #e2e8f0">
                        <p style="margin:0 0 16px">Hei <strong>{customer.CompanyName}</strong>,</p>
                        <p style="margin:0 0 16px">Teille on laadittu uusi lasku HakaTech-portaaliin.</p>
                        <table style="width:100%;border-collapse:collapse;margin-bottom:20px">
                          <tr><td style="padding:8px 0;color:#64748b;width:160px">Laskunumero</td>
                              <td style="padding:8px 0;font-weight:600">{invoice.InvoiceNumber}</td></tr>
                          <tr><td style="padding:8px 0;color:#64748b">Laskupäivä</td>
                              <td style="padding:8px 0">{invoice.InvoiceDate:dd.MM.yyyy}</td></tr>
                          <tr><td style="padding:8px 0;color:#64748b">Eräpäivä</td>
                              <td style="padding:8px 0;font-weight:600;color:#dc2626">{invoice.DueDate:dd.MM.yyyy}</td></tr>
                          <tr style="border-top:2px solid #e2e8f0">
                              <td style="padding:12px 0 8px;color:#64748b">Yhteensä (sis. ALV)</td>
                              <td style="padding:12px 0 8px;font-size:18px;font-weight:700">
                                {invoice.TotalAmount:N2} €</td></tr>
                        </table>
                        <p style="margin:0 0 20px">Voitte tarkastella laskua kirjautumalla HakaTech-portaaliin.</p>
                        <p style="margin:0;color:#94a3b8;font-size:13px">
                          Tämä on automaattinen viesti. Älä vastaa tähän sähköpostiin.<br>
                          HakaTech IT-palvelut | asiakastuki@hakatech.fi
                        </p>
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
                // Sähköpostin lähetys ei saa estää laskun tallentamista.
                _logger.LogWarning(ex,
                    "Laskuilmoituksen lähetys epäonnistui (lasku {Num}).", invoice.InvoiceNumber);
            }
        }

        // Onnistumisviesti kertoo myös lähetettiinkö sähköposti.
        // Kolmiosainen lauseke: jos asiakas ja osoite on olemassa, lisätään " ja lähetetty..." -osa.
        TempData["SuccessMessage"] = $"Lasku {invoice.InvoiceNumber} luotu" +
            (customer is not null && !string.IsNullOrWhiteSpace(customer.ContactEmail)
                ? $" ja lähetetty osoitteeseen {customer.ContactEmail}."
                : ".");
        // Ohjataan luodun laskun tietosivulle.
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    // ── POST /Invoice/UpdateStatus ───────────────────────────────────────────
    // Admin päivittää laskun tilan (esim. Draft → Sent, Sent → Paid).
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(InvoiceStatusViewModel model)
    {
        // Haetaan lasku pääavaimella — pelkkä tila tarvitaan päivitykseen.
        var invoice = await _db.Invoices.FindAsync(model.Id);
        if (invoice is null) return NotFound();

        // Päivitetään laskun tila.
        invoice.Status = model.Status;

        // Erityistapaus: Paid-tilaan siirtyessä tallennetaan maksupäivä.
        // "??" = jos admin ei antanut päivämäärää, käytetään nykyistä UTC-aikaa.
        if (model.Status == InvoiceStatus.Paid)
            invoice.PaidAt = model.PaidAt ?? DateTime.UtcNow;
        // Jos tila ei ole enää Paid (esim. palautettu Sent), poistetaan maksupäivä.
        else if (model.Status != InvoiceStatus.Paid)
            invoice.PaidAt = null;

        await _db.SaveChangesAsync(); // UPDATE SQL.
        _logger.LogInformation("Laskun {Num} tila → {Status}.", invoice.InvoiceNumber, invoice.Status);
        TempData["SuccessMessage"] = "Laskun tila päivitetty.";
        // Ohjataan takaisin laskun tietosivulle.
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    // ── POST /Invoice/UploadAttachment ───────────────────────────────────────
    // Admin lisää liitteen laskuun (esim. kuitti, erittelyliite).
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAttachment(
        int invoiceId,  // Laskun ID johon liite lisätään.
        IFormFile file) // Ladattu tiedosto HTTP-pyynnöstä.
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        // Tarkistetaan lasku olemassaolo — FindAsync on nopein haku pääavaimella.
        var invoice = await _db.Invoices.FindAsync(invoiceId);
        if (invoice is null) return NotFound();

        // Tarkistetaan ettei tiedosto ole tyhjä tai puuttuva.
        if (file is null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Tiedosto on tyhjä tai puuttuu.";
            return RedirectToAction(nameof(Details), new { id = invoiceId });
        }

        // Kokorajoitus: 20 MB = 20 * 1024 * 1024 tavua.
        // Liian suuret tiedostot hidastavat sivun latautumista ja kuluttavat levytilaa.
        if (file.Length > 20 * 1024 * 1024)
        {
            TempData["ErrorMessage"] = "Tiedosto on liian suuri (max 20 MB).";
            return RedirectToAction(nameof(Details), new { id = invoiceId });
        }

        // Sallitut tiedostopäätteet whitelist-periaatteella.
        // StringComparer.OrdinalIgnoreCase = ei eroa isojen/pienten kirjainten välillä
        // (.PDF ja .pdf molemmat sallitaan).
        var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".png", ".jpg", ".jpeg", ".gif", ".zip" };
        // Path.GetExtension() palauttaa tiedostopäätteen, esim. ".pdf".
        var ext = Path.GetExtension(file.FileName);
        // Tarkistetaan ettei päätettä puutu ja että se on sallittu.
        if (string.IsNullOrEmpty(ext) || !allowedExt.Contains(ext))
        {
            TempData["ErrorMessage"] = $"Tiedostotyyppi '{ext}' ei ole sallittu.";
            return RedirectToAction(nameof(Details), new { id = invoiceId });
        }

        // Tallennetaan tiedosto levylle tiedostopalvelun kautta.
        // "invoices" = alikansio uploads-hakemistossa, palautetaan tallennuspolku.
        var filePath = await _fileStorage.SaveFileAsync(file, "invoices");

        // Luodaan liitetietue tietokantaan.
        _db.InvoiceAttachments.Add(new InvoiceAttachment
        {
            InvoiceId        = invoiceId,
            // Path.GetFileName() palauttaa vain tiedostonimen ilman polkua (turvallisuus).
            FileName         = Path.GetFileName(file.FileName),
            FilePath         = filePath,         // Polku levyllä.
            UploadedAt       = DateTime.UtcNow,
            UploadedByUserId = currentUser.Id    // Kuka latasi — auditointia varten.
        });
        await _db.SaveChangesAsync(); // INSERT SQL.

        TempData["SuccessMessage"] = $"Tiedosto '{Path.GetFileName(file.FileName)}' lisätty.";
        return RedirectToAction(nameof(Details), new { id = invoiceId });
    }

    // ── GET /Invoice/DownloadAttachment/5 ────────────────────────────────────
    // Palauttaa laskun liitteen ladattavana tiedostona.
    [HttpGet]
    public async Task<IActionResult> DownloadAttachment(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        // Haetaan liitetietue laskun tiedoilla (tarvitaan IDOR-tarkistukseen).
        var attachment = await _db.InvoiceAttachments
            .Include(a => a.Invoice) // Tarvitaan Invoice.CustomerId IDOR-tarkistukseen.
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attachment is null) return NotFound();

        // IDOR-suoja: asiakas saa ladata vain oman yrityksensä laskujen liitteet.
        if (!isAdmin && attachment.Invoice?.CustomerId != currentUser?.CustomerId)
            return Forbid();

        // Muodostetaan turvallinen tiedostopolku levyllä.
        // ResolveSafePath tarkistaa ettei polku ylitä sallittua uploads-hakemistoa (path traversal -suoja).
        var fullPath = _fileStorage.ResolveSafePath(attachment.FilePath);
        // Tarkistetaan että tiedosto löytyy levyltä (se voisi olla poistettu manuaalisesti).
        if (fullPath is null || !System.IO.File.Exists(fullPath))
            return NotFound();

        // FileExtensionContentTypeProvider määrittää MIME-tyypin tiedostopäätteen perusteella.
        // Esim. ".pdf" → "application/pdf", ".xlsx" → "application/vnd.openxmlformats-..."
        var provider = new FileExtensionContentTypeProvider();
        // TryGetContentType: jos MIME-tyyppiä ei tunnisteta, käytetään yleistä binäärivirtatyyppiä.
        // "application/octet-stream" = "tuntematon binääritiedosto" — selain tarjoaa latausta.
        if (!provider.TryGetContentType(attachment.FileName, out var contentType))
            contentType = "application/octet-stream";

        // PhysicalFile() = palauttaa tiedoston suoraan levyltä muistiin kopioimatta.
        // Kolmas parametri = Content-Disposition-otsikko: ehdotettu tiedostonimi selaimelle.
        return PhysicalFile(fullPath, contentType, attachment.FileName);
    }

    // ── POST /Invoice/DeleteAttachment/5 ────────────────────────────────────
    // Admin poistaa laskun liitteen sekä tietokannasta että levyltä.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttachment(int id)
    {
        // Haetaan liitetietue — FindAsync on nopein pääavainhaulla.
        var attachment = await _db.InvoiceAttachments.FindAsync(id);
        if (attachment is null) return NotFound();

        // Tallennetaan laskun ID ennen poistoa — tarvitaan ohjaukseen poiston jälkeen.
        int invoiceId = attachment.InvoiceId;
        // Poistetaan tiedosto levyltä ensin.
        _fileStorage.DeleteFile(attachment.FilePath);
        // Poistetaan liitetietue tietokannasta.
        _db.InvoiceAttachments.Remove(attachment);
        await _db.SaveChangesAsync(); // DELETE SQL.

        TempData["SuccessMessage"] = "Liite poistettu.";
        // Ohjataan takaisin laskun tietosivulle.
        return RedirectToAction(nameof(Details), new { id = invoiceId });
    }

    // ── Yksityiset apumetodit ────────────────────────────────────────────────

    // Rakentaa pudotusvalikon aktiivisista asiakkaista.
    // "private async Task<>" = yksityinen, asynkroninen, palauttaa kokoelman.
    // IEnumerable<SelectListItem> = pudotusvalikon vaihtoehtojen lista.
    private async Task<IEnumerable<SelectListItem>> BuildCustomerOptions() =>
        (await _db.Customers
            .Where(c => c.IsActive)            // Vain aktiiviset asiakkaat.
            .OrderBy(c => c.CompanyName)       // Aakkosjärjestys.
            .ToListAsync())
        // Muunnetaan SelectListItem-olioiksi: näytettävä teksti ja arvo.
        // SelectListItem(text, value) — value tallennetaan lomakkeeseen.
        .Select(c => new SelectListItem(c.CompanyName, c.Id.ToString()));

    // Ehdottaa seuraavaa vapaata laskunumeroa muodossa INV-YYYY-NNN.
    // Esim. vuonna 2025 10. lasku → "INV-2025-010".
    private async Task<string> SuggestInvoiceNumber()
    {
        int year = DateTime.Today.Year;
        // Lasketaan tämän vuoden laskujen määrä.
        // CountAsync() = SQL:n COUNT(*) WHERE InvoiceDate.Year = year.
        int count = await _db.Invoices
            .CountAsync(i => i.InvoiceDate.Year == year);
        // :D3 = muotoilu joka täyttää nollilla kolmeen merkkiin: 1 → "001", 42 → "042".
        return $"INV-{year}-{(count + 1):D3}";
    }
}
