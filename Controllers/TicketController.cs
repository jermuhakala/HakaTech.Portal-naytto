// Nimiavaruuksien tuonnit.
using HakaTech.Portal.Data;                        // ApplicationDbContext — tietokantayhteys.
using HakaTech.Portal.Models.Domain;               // Ticket, TicketComment, TicketAttachment, TicketFeedback...
using HakaTech.Portal.Models.ViewModels;           // TicketCreateViewModel, TicketEditViewModel...
using HakaTech.Portal.Services;                    // IEmailService, IFileStorageService, IAuditService.
using Microsoft.AspNetCore.Authorization;          // [Authorize]-attribuutti.
using Microsoft.AspNetCore.Identity;               // UserManager.
using Microsoft.AspNetCore.Mvc;                    // Controller, IActionResult, TempData...
using Microsoft.AspNetCore.Mvc.Rendering;          // SelectListItem — pudotusvalikon vaihtoehto.
using Microsoft.AspNetCore.StaticFiles;            // FileExtensionContentTypeProvider — MIME-tyyppi.
using Microsoft.EntityFrameworkCore;               // Include, ToListAsync, FindAsync...

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Tikettien (tukipyyntöjen) controller. Vastaa tikettien luomisesta,
/// listaamisesta, kommenttien lisäämisestä ja liitteistä. Asiakas näkee
/// vain oman yrityksensä tiketit, admin näkee kaikki ja voi hallinnoida
/// tilaa, prioriteettia ja vastuuhenkilöä.
///
/// Tärkeitä toimintoja:
///  - Index, Create, Details — perustoiminnot
///  - UpdateStatus / Close — tiketin tilan hallinta
///  - AddComment / UploadAttachment — keskustelu ja liitteet
///  - SubmitFeedback — asiakas antaa palautteen suljetusta tiketistä
/// </summary>
// [Authorize] = kirjautuminen vaaditaan kaikissa action-metodeissa.
[Authorize]
public class TicketController : Controller
{
    // Tietokantayhteys.
    private readonly ApplicationDbContext         _db;
    // UserManager — haetaan kirjautunut käyttäjä ja roolit.
    private readonly UserManager<ApplicationUser> _userManager;
    // Diagnostiikkaloki kehittäjälle.
    private readonly ILogger<TicketController>    _logger;
    // Sähköpostipalvelu — tilamuutosilmoitukset asiakkaalle.
    private readonly IEmailService                _emailService;
    // Tiedostopalvelu — liitteiden tallentaminen levylle.
    private readonly IFileStorageService          _fileStorage;
    // Web-hostin ympäristö — tiedostopolkuja varten.
    private readonly IWebHostEnvironment          _env;
    // Auditointipalvelu — tiketin luonti, tilamuutokset ja palaute lokitetaan.
    private readonly IAuditService                _audit;

    // Konstruktori: DI-säiliö täyttää kaikki parametrit.
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

    // ── GET /Ticket ──────────────────────────────────────────────────────────
    // Tikettilista. Admin näkee kaikki, asiakas vain omansa. Suodattimet: asiakas,
    // tila, prioriteetti, vapaa tekstihaku.
    public async Task<IActionResult> Index(
        int?           customerId, // Asiakassuodatin (vain admin). null = kaikki.
        TicketStatus?  status,     // Tilasuodatin. null = kaikki tilat.
        TicketPriority? priority,  // Prioriteettisuodatin. null = kaikki prioriteetit.
        string?        search)     // Vapaa tekstihaku otsikossa ja kuvauksessa.
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        // Aloitetaan kyselyobjektilla jolle lisätään ehtoja alla.
        // Include() lataa liittyvät navigaatio-ominaisuudet JOIN-kyselynä.
        var query = _db.Tickets
            .Include(t => t.Customer)       // Yrityksen nimi listaksi.
            .Include(t => t.CreatedByUser)  // Luojan nimi.
            .Include(t => t.AssignedToUser) // Vastuuhenkilön nimi.
            .AsQueryable();

        // Asiakaskäyttäjä näkee vain oman yrityksensä tiketit.
        if (!isAdmin)
        {
            // Jos CustomerId puuttuu, palautetaan tyhjä lista — ei virhettä.
            if (currentUser?.CustomerId is null)
                return View(Enumerable.Empty<Ticket>());

            query = query.Where(t => t.CustomerId == currentUser.CustomerId);
        }
        // Admin voi suodattaa tietyn asiakkaan tiketit.
        else if (customerId.HasValue)
        {
            query = query.Where(t => t.CustomerId == customerId.Value);
        }

        // Tilasuodatin — lisätään vain jos annettu.
        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        // Prioriteettisuodatin.
        if (priority.HasValue)
            query = query.Where(t => t.Priority == priority.Value);

        // Tekstihaku otsikosta ja kuvauksesta.
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim(); // Poistetaan ylimääräiset välilyönnit.
            // Contains() = SQL LIKE '%hakusana%' — osittainen vastaavuus.
            query = query.Where(t =>
                t.Title.Contains(search) ||
                t.Description.Contains(search));
        }

        // Haetaan tiketit uusimmasta vanhimpaan.
        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        // Välitetään suodattimet näkymälle (lomake pysyy täytettynä palautuksella).
        ViewBag.StatusFilter   = status;
        ViewBag.PriorityFilter = priority;
        ViewBag.Search         = search;
        ViewBag.CustomerFilter = customerId;
        // Roolitieto näkymää varten — admin näkee enemmän sarakkeita ja toimintoja.
        ViewBag.IsAdmin        = isAdmin;

        return View(tickets);
    }

    // ── GET /Ticket/Details/5 ────────────────────────────────────────────────
    // Yksittäisen tiketin tiedot, kommentit, liitteet ja palautelomake.
    public async Task<IActionResult> Details(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        // Haetaan tiketti kaikilla tarvittavilla suhteilla.
        // OrderBy() Include:n sisällä = SQL ORDER BY alitaulun mukaan.
        var ticket = await _db.Tickets
            .Include(t => t.Customer)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            // Kommentit aikajärjestyksessä — ladataan myös kirjoittajan tiedot.
            .Include(t => t.Comments.OrderBy(c => c.CreatedAt))
                .ThenInclude(c => c.Author)
            // Liitteet latausaikajärjestyksessä — ladataan lataajan tiedot.
            .Include(t => t.Attachments.OrderBy(a => a.UploadedAt))
                .ThenInclude(a => a.UploadedByUser)
            .Include(t => t.Feedback) // Asiakkaan palaute (one-to-one).
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket is null)
            return NotFound();

        // IDOR-suoja: asiakas saa nähdä vain oman yrityksensä tiketit.
        if (!isAdmin && ticket.CustomerId != currentUser?.CustomerId)
            return Forbid(); // HTTP 403.

        // Välitetään tietoja näkymälle ViewBagin kautta.
        ViewBag.IsAdmin      = isAdmin;
        ViewBag.CurrentUser  = currentUser;
        // Esitäytetty kommentointilomakkeen ViewModel — TicketId mukana piilokenttänä.
        ViewBag.CommentModel = new TicketCommentViewModel { TicketId = id };
        // Adminin muokkauslomake (tila, prioriteetti, vastuuhenkilö).
        // Null asiakkaalle — asiakas ei näe muokkauslomaketta.
        ViewBag.EditModel    = isAdmin ? await BuildEditViewModel(ticket) : null;
        // Näytetäänkö palautelomake?
        // Ehdot: ei admin, tiketti suljettu, ei vielä palautetta, tiketin luonut käyttäjä.
        ViewBag.ShowFeedback = !isAdmin
                               && ticket.Status == TicketStatus.Closed
                               && ticket.Feedback is null           // Ei vielä palautetta.
                               && ticket.CreatedByUserId == currentUser?.Id; // Oma tiketti.

        return View(ticket);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TIKETIN LUONTI — kahden vaiheen putki:
    //
    //   1) GET /Ticket/Create   → Näyttää tyhjän lomakkeen
    //   2) POST /Ticket/Create  → Vastaanottaa täytetyn lomakkeen,
    //                             tallentaa tiketin ja ohjaa detaljisivulle
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GET /Ticket/Create — Näyttää tyhjän tiketin luontilomakkeen.
    /// Adminille näytetään asiakaspudotusvalikko, asiakaskäyttäjälle
    /// asiakkuus täytetään automaattisesti omasta käyttäjäprofiilista.
    /// </summary>
    /// <param name="customerId">Vapaaehtoinen esivalinta adminille (esim. tultaessa asiakassivulta).</param>
    public async Task<IActionResult> Create(int? customerId)
    {
        // Haetaan kirjautuneen käyttäjän tiedot.
        var currentUser = await _userManager.GetUserAsync(User);
        // Onko käyttäjä admin-roolissa? Vaikuttaa lomakkeen näkymään.
        bool isAdmin    = User.IsInRole("Admin");

        // Luodaan tyhjä ViewModel oletusarvoilla.
        var model = new TicketCreateViewModel
        {
            Category        = TicketCategory.Other,    // Oletuskategoria.
            Priority        = TicketPriority.Normal,   // Oletusprioriteetti.
            CustomerOptions = []                        // Tyhjä lista — täytetään vain adminille alla.
        };

        if (isAdmin)
        {
            // Adminille esitäytetään mahdollinen ennaltavalittu asiakas
            // ja rakennetaan asiakaspudotusvalikon vaihtoehdot.
            model.CustomerId      = customerId;
            model.CustomerOptions = await BuildCustomerOptions();
        }
        else
        {
            // Asiakaskäyttäjälle asiakas tulee suoraan käyttäjän omasta yrityssidostuksesta.
            // Lomakkeen kentästä asiakas EI voi vaihtaa toista yritystä (turvallisuus).
            model.CustomerId = currentUser?.CustomerId;
        }

        // Palautetaan View: ASP.NET renderöi Views/Ticket/Create.cshtml
        // ja antaa sille parametrina tämän model-olion.
        return View(model);
    }

    /// <summary>
    /// POST /Ticket/Create — Vastaanottaa lomakkeen tiedot, validoi, tallentaa tiketin
    /// tietokantaan ja ohjaa onnistumisen jälkeen tiketin detaljisivulle.
    ///
    /// Suojaukset:
    ///  - [ValidateAntiForgeryToken] tarkistaa CSRF-tokenin → estää ulkopuolisten sivustojen
    ///    väärennetyt POST-pyynnöt käyttäjän selaimen kautta.
    ///  - ModelState validoi DataAnnotations-säännöt (pakolliset kentät, pituudet).
    ///  - Asiakaskäyttäjän CustomerId pakotetaan palvelinpuolella omaksi yritykseksi.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TicketCreateViewModel model)
    {
        // ASP.NET on jo täyttänyt 'model'-olion automaattisesti lomakkeen kentistä.
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        // ── TIETOTURVA: yliajetaan asiakaskäyttäjän CustomerId ──────────────
        // Asiakas EI voi luoda tikettiä toisen yrityksen nimissä.
        // Lomakkeesta tulleen CustomerId:n sijaan käytetään aina käyttäjän omaa yritystä.
        if (!isAdmin)
            model.CustomerId = currentUser?.CustomerId;

        // Tarkistetaan että asiakas on valittu.
        if (model.CustomerId is null)
            ModelState.AddModelError(nameof(model.CustomerId), "Asiakas on valittava.");

        // ModelState.IsValid tarkistaa DataAnnotations-attribuutit ja yllä lisätyt virheet.
        if (!ModelState.IsValid)
        {
            // Validointi epäonnistui → palautetaan lomakkeelle virheineen.
            if (isAdmin) model.CustomerOptions = await BuildCustomerOptions();
            return View(model);
        }

        // ── Tiketin entiteetin rakentaminen ──────────────────────────────────
        // Muunnetaan ViewModelista Ticket-entiteetti tietokantaa varten.
        var ticket = new Ticket
        {
            Title            = model.Title,
            Description      = model.Description,
            Category         = model.Category,
            Priority         = model.Priority,
            Status           = TicketStatus.Open,       // Uusi tiketti on aina avoin.
            CustomerId       = model.CustomerId!.Value, // "!" = null-tarkistus tehty yllä.
            CreatedByUserId  = currentUser!.Id,         // Tiketin luonut käyttäjä.
            CreatedAt        = DateTime.UtcNow,         // Aikaleimat tallennetaan UTC:nä.
            UpdatedAt        = DateTime.UtcNow
        };

        // Lisätään EF Coren seurantaan ja tallennetaan kantaan.
        // SaveChangesAsync() generoi INSERT SQL:n ja täyttää ticket.Id automaattisesti.
        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        // Lokitus ja auditijälki.
        _logger.LogInformation("Tiketti #{Id} '{Title}' luotu.", ticket.Id, ticket.Title);
        await _audit.LogAsync("TicketCreated", "Ticket", ticket.Id.ToString(), ticket.Title);

        // TempData säilyy yhden uudelleenohjauk sen yli — näytetään seuraavalla sivulla.
        TempData["SuccessMessage"] = $"Tiketti #{ticket.Id} luotu onnistuneesti.";
        // Ohjataan luodun tiketin tietosivulle.
        return RedirectToAction(nameof(Details), new { id = ticket.Id });
    }

    // ── POST /Ticket/UpdateStatus (Admin) ────────────────────────────────────
    // Admin muuttaa tiketin tilaa, prioriteettia ja vastuuhenkilöä.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(TicketEditViewModel model)
    {
        // Haetaan tiketti — Include(t.CreatedByUser) tarvitaan sähköpostilähetykseen.
        var ticket = await _db.Tickets
            .Include(t => t.CreatedByUser)
            .FirstOrDefaultAsync(t => t.Id == model.Id);
        if (ticket is null) return NotFound();

        // Tallennetaan vanha tila auditlokia varten (ennen → jälkeen).
        var oldStatus = ticket.Status;

        // Tarkistetaan vastuuhenkilö — sen täytyy olla Admin-roolissa.
        // Asiakaskäyttäjää ei voi asettaa tiketin vastuuhenkilöksi.
        if (!string.IsNullOrEmpty(model.AssignedToUserId))
        {
            var assignee = await _userManager.FindByIdAsync(model.AssignedToUserId);
            // FindByIdAsync palauttaa null jos käyttäjää ei löydy.
            if (assignee is null || !await _userManager.IsInRoleAsync(assignee, "Admin"))
            {
                TempData["ErrorMessage"] = "Vastuuhenkilön tulee olla Admin-roolissa.";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
        }

        // Päivitetään tiketin kentät.
        ticket.Status           = model.Status;
        ticket.Priority         = model.Priority;
        ticket.AssignedToUserId = model.AssignedToUserId; // null = poistetaan vastuuhenkilö.
        ticket.UpdatedAt        = DateTime.UtcNow;

        // Resolved-aika: asetetaan kun tiketti merkitään ratkaistuksi.
        // "is null" varmistaa ettei ylikirjoiteta olemassaolevaa ratkaisuaikaa.
        if (model.Status == TicketStatus.Resolved && ticket.ResolvedAt is null)
            ticket.ResolvedAt = DateTime.UtcNow;
        // Jos tila palautetaan aiemmaksi (esim. uudelleenavataan), poistetaan ratkaisuaika.
        else if (model.Status != TicketStatus.Resolved && model.Status != TicketStatus.Closed)
            ticket.ResolvedAt = null;

        await _db.SaveChangesAsync(); // UPDATE SQL.

        // Lähetetään tilamuutosilmoitus sähköpostilla kun tila muuttuu merkittävästi.
        // Ehdot: vanha tila ei ole sama kuin uusi (vältetään turhat sähköpostit)
        // JA uusi tila on joko InProgress tai Closed (merkittävät muutokset).
        if (oldStatus != model.Status &&
            (model.Status == TicketStatus.InProgress || model.Status == TicketStatus.Closed))
            await SendTicketStatusEmailAsync(
                ticket,
                model.Status == TicketStatus.InProgress ? "Otettu työn alle" : "Suljettu");

        _logger.LogInformation("Tiketin #{Id} tila muutettu → {Status}.", ticket.Id, ticket.Status);
        // Auditloki: kirjataan muutos muodossa "Open → InProgress".
        await _audit.LogAsync("TicketStatusChanged", "Ticket", ticket.Id.ToString(),
            $"{oldStatus} → {ticket.Status}");
        TempData["SuccessMessage"] = "Tiketin tila päivitetty.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    // ── POST /Ticket/AddComment ──────────────────────────────────────────────
    // Lisää kommentin tikettiin. Sekä asiakas että admin voivat kommentoida.
    // Admin voi lisätä sisäisiä muistiinpanoja (IsInternal=true) jotka asiakas ei näe.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(TicketCommentViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        // Tarkistetaan tiketin olemassaolo.
        var ticket = await _db.Tickets.FindAsync(model.TicketId);
        if (ticket is null) return NotFound();

        // Turvallisuus: vain admin voi lisätä sisäisiä muistiinpanoja.
        // Asiakaskäyttäjän IsInternal-kentän arvo ylikirjoitetaan false-arvolla.
        if (!isAdmin) model.IsInternal = false;

        // Jos validointi epäonnistuu (esim. tyhjä kommentti), ohjataan takaisin detaljisivulle.
        // Virheilmoitukset näytetään ModelState-tilanteen perusteella.
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Details), new { id = model.TicketId });

        // Luodaan uusi kommenttientiteetti.
        var comment = new TicketComment
        {
            TicketId   = model.TicketId,
            Content    = model.Content,
            IsInternal = model.IsInternal, // false = näkyy asiakkaalle, true = vain admineille.
            AuthorId   = currentUser!.Id,  // "!" = käyttäjä on aina kirjautunut ([Authorize]).
            CreatedAt  = DateTime.UtcNow
        };

        // Päivitetään tiketin muokkausaika — tiketti näyttää aktiiviselta listanäkymässä.
        ticket.UpdatedAt = DateTime.UtcNow;

        // Asiakkaan kommentti palauttaa tiketin InProgress-tilaan jos se odotti vastausta.
        // WaitingCustomer-tila tarkoittaa: "Admin odottaa asiakkaalta lisätietoja".
        // Kun asiakas kommentoi, tiketti palaa työn alle.
        if (!isAdmin && ticket.Status == TicketStatus.WaitingCustomer)
            ticket.Status = TicketStatus.InProgress;

        _db.TicketComments.Add(comment);
        await _db.SaveChangesAsync(); // INSERT SQL.

        // Ohjataan takaisin tikettisivulle — kommentti näkyy nyt listassa.
        return RedirectToAction(nameof(Details), new { id = model.TicketId });
    }

    // ── POST /Ticket/Close ───────────────────────────────────────────────────
    // Sulkee tiketin. Asiakas voi sulkea oman tikettinsä, admin voi sulkea kenen tahansa.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        // Haetaan tiketti — Include tarvitaan sähköpostilähetykseen.
        var ticket = await _db.Tickets
            .Include(t => t.CreatedByUser)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();

        // Turvallisuus: asiakas voi sulkea vain oman tikettinsä.
        // CreatedByUserId = tiketin luoneen käyttäjän ID.
        if (!isAdmin && ticket.CreatedByUserId != currentUser?.Id)
            return Forbid(); // HTTP 403.

        var oldStatus    = ticket.Status;
        ticket.Status    = TicketStatus.Closed;
        ticket.UpdatedAt = DateTime.UtcNow;
        // "??=" = null-yhdistämisoperaattori sijoituksessa:
        // Jos ResolvedAt on null (ei vielä ratkaistu), aseta se nyt.
        // Jos jo asetettu, jätetään alkuperäinen arvo.
        ticket.ResolvedAt ??= DateTime.UtcNow;

        await _db.SaveChangesAsync(); // UPDATE SQL.

        // Adminin sulkeminen lähettää sähköpostin asiakkaalle.
        // Asiakkaan itse sulkeminen ei lähetä (turha sähköposti itselleen).
        if (oldStatus != TicketStatus.Closed && isAdmin)
            await SendTicketStatusEmailAsync(ticket, "Suljettu");

        await _audit.LogAsync("TicketClosed", "Ticket", id.ToString());
        TempData["SuccessMessage"] = $"Tiketti #{id} suljettu.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── POST /Ticket/UploadAttachment ────────────────────────────────────────
    // Lisää liitteen tikettiin. Sekä asiakas (oman tikettinsä) että admin voi ladata.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAttachment(
        int ticketId,   // Tiketin ID johon liite lisätään.
        IFormFile file) // Ladattu tiedosto HTTP-pyynnöstä.
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        var ticket = await _db.Tickets.FindAsync(ticketId);
        if (ticket is null) return NotFound();

        // IDOR-suoja: asiakas voi lisätä liitteen vain oman yrityksensä tikettiin.
        if (!isAdmin && ticket.CustomerId != currentUser?.CustomerId)
            return Forbid();

        // Käyttäjätarkistus — pitäisi olla mahdotonta [Authorize]-attribuutin takia.
        if (currentUser is null) return Unauthorized();

        // Tarkistetaan ettei tiedosto ole tyhjä.
        if (file is null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Tiedosto on tyhjä tai puuttuu.";
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        // Kokorajoitus: 20 MB = 20 × 1024 × 1024 tavua.
        if (file.Length > 20 * 1024 * 1024)
        {
            TempData["ErrorMessage"] = "Tiedosto on liian suuri (max 20 MB).";
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        // Sallitut tiedostopäätteet whitelist-periaatteella.
        // StringComparer.OrdinalIgnoreCase = kirjainkoko ei vaikuta (.PDF == .pdf).
        var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".png", ".jpg", ".jpeg", ".gif", ".zip" };
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !allowedExt.Contains(ext))
        {
            TempData["ErrorMessage"] = $"Tiedostotyyppi '{ext}' ei ole sallittu.";
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        // Tallennetaan tiedosto levylle — "tickets" on kohdealihaketmisto.
        var filePath = await _fileStorage.SaveFileAsync(file, "tickets");

        // Luodaan liitetietue tietokantaan.
        _db.TicketAttachments.Add(new TicketAttachment
        {
            TicketId         = ticketId,
            FileName         = Path.GetFileName(file.FileName), // Vain tiedostonimi, ei polkua.
            FilePath         = filePath,     // Polku levyllä (suhteellinen tai absoluuttinen).
            UploadedAt       = DateTime.UtcNow,
            UploadedByUserId = currentUser.Id // Kuka latasi — auditointia varten.
        });
        await _db.SaveChangesAsync(); // INSERT SQL.

        TempData["SuccessMessage"] = $"Tiedosto '{Path.GetFileName(file.FileName)}' lisätty.";
        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    // ── GET /Ticket/DownloadAttachment/5 ────────────────────────────────────
    // Palauttaa tiketin liitteen ladattavana tiedostona.
    [HttpGet]
    public async Task<IActionResult> DownloadAttachment(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        // Haetaan liitetietue tiketin tiedoilla (IDOR-tarkistusta varten).
        var attachment = await _db.TicketAttachments
            .Include(a => a.Ticket) // Tarvitaan Ticket.CustomerId -tarkistukseen.
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attachment is null) return NotFound();

        // IDOR-suoja: asiakas voi ladata vain oman yrityksensä tikettien liitteet.
        if (!isAdmin && attachment.Ticket?.CustomerId != currentUser?.CustomerId)
            return Forbid();

        // Muodostetaan turvallinen tiedostopolku (estää path traversal -hyökkäykset).
        var fullPath = _fileStorage.ResolveSafePath(attachment.FilePath);
        // Tarkistetaan fyysinen olemassaolo levyllä.
        if (fullPath is null || !System.IO.File.Exists(fullPath))
            return NotFound();

        // Tunnistetaan MIME-tyyppi tiedostopäätteen perusteella.
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(attachment.FileName, out var contentType))
            contentType = "application/octet-stream"; // Tuntematon binääri.

        // PhysicalFile() palauttaa tiedoston suoraan levyltä.
        return PhysicalFile(fullPath, contentType, attachment.FileName);
    }

    // ── POST /Ticket/DeleteAttachment/5 (Admin) ──────────────────────────────
    // Admin poistaa tiketin liitteen sekä tietokannasta että levyltä.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttachment(int id)
    {
        var attachment = await _db.TicketAttachments.FindAsync(id);
        if (attachment is null) return NotFound();

        // Tallennetaan ticketId ennen poistoa — tarvitaan uudelleenohjaukseen.
        int ticketId = attachment.TicketId;
        _fileStorage.DeleteFile(attachment.FilePath); // Poistetaan tiedosto levyltä.
        _db.TicketAttachments.Remove(attachment);     // Poistetaan tietue tietokannasta.
        await _db.SaveChangesAsync(); // DELETE SQL.

        TempData["SuccessMessage"] = "Liite poistettu.";
        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    // ── POST /Ticket/SubmitFeedback ──────────────────────────────────────────
    // Asiakas antaa palautteen suljetusta tiketistä (tähtiluokitus + kommentti).
    // Voidaan antaa vain kerran (unique index estää duplikaatit).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitFeedback(
        int ticketId,   // Minkä tiketin palaute.
        int rating,     // Tähtiluokitus 1–5.
        string? comment) // Vapaaehtoinen tekstikommentti.
    {
        // Validoi että luokitus on välillä 1–5.
        // BadRequest() = HTTP 400 — pyyntö on virheellinen.
        if (rating < 1 || rating > 5)
            return BadRequest();

        var currentUser = await _userManager.GetUserAsync(User);
        var ticket      = await _db.Tickets
            .Include(t => t.Feedback) // Tarvitaan tarkistukseen: onko jo palaute annettu.
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket is null) return NotFound();
        // Jos palaute on jo annettu, ohjataan takaisin tikettisivulle (ei anneta uudestaan).
        if (ticket.Feedback is not null) return RedirectToAction(nameof(Details), new { id = ticketId });
        // Palautetta voi antaa vain suljetusta tiketistä.
        if (ticket.Status != TicketStatus.Closed) return RedirectToAction(nameof(Details), new { id = ticketId });
        // Vain tiketin luonut käyttäjä voi antaa palautteen (ei muut yrityksen käyttäjät).
        if (ticket.CreatedByUserId != currentUser?.Id) return Forbid();

        // Luodaan palauteentiteetti.
        _db.TicketFeedbacks.Add(new TicketFeedback
        {
            TicketId    = ticketId,
            UserId      = currentUser.Id,
            Rating      = rating,
            Comment     = comment?.Trim(), // "?" = trim vain jos kommentti ei ole null.
            SubmittedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(); // INSERT SQL.

        // Kirjataan palaute auditlokiin — tärkeää asiakastyytyväisyyden seurannalle.
        await _audit.LogAsync("FeedbackSubmitted", "Ticket", ticketId.ToString(),
            $"Rating: {rating}");

        TempData["SuccessMessage"] = "Kiitos palautteestasi!";
        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    // ── Yksityiset apumetodit ────────────────────────────────────────────────

    // Lähettää sähköposti-ilmoituksen tiketin tilan muuttumisesta sen luojalle.
    // "private async Task" = palauttaa ei mitään (void), mutta asynkronisesti.
    private async Task SendTicketStatusEmailAsync(Ticket ticket, string statusText)
    {
        // Jos tiketillä ei ole luojaa tai sähköpostiosoitetta, ei lähetetä.
        if (ticket.CreatedByUser == null || string.IsNullOrWhiteSpace(ticket.CreatedByUser.Email))
            return;

        // Käytetään koko nimeä jos se on saatavilla, muuten sähköpostiosoitetta.
        string name = !string.IsNullOrWhiteSpace(ticket.CreatedByUser.FullName)
            ? ticket.CreatedByUser.FullName
            : ticket.CreatedByUser.Email;

        // Rakennetaan sähköpostin aihe ja HTML-runko.
        string subject     = $"Tiketti #{ticket.Id} on {statusText.ToLower()}";
        // @"..." = verbatim string literal — ei tulkita escape-sekvenssejä (paitsi "").
        string htmlMessage = $@"
            <div style=""font-family: Arial, sans-serif; color: #333;"">
                <h3 style=""color: #2b5797;"">Hei {name}!</h3>
                <p>Tukipyyntösi (Tiketti #{ticket.Id}: <strong>{ticket.Title}</strong>)
                   tila on muuttunut.</p>
                <p>Uusi tila: <strong style=""padding: 3px 6px; background-color: #f1f5f9;
                   border-radius: 4px;"">{statusText}</strong></p>
                <p>Kirjaudu HakaTech Portaaliin tarkastellaksesi tiketin tietoja
                   ja mahdollisia vastauksia.</p>
                <br/><hr style=""border: none; border-top: 1px solid #ddd;""/>
                <p style=""font-size: 0.9em; color: #777;"">Ystävällisin terveisin,<br/>
                <strong>HakaTech Asiakastuki</strong></p>
            </div>";

        // Lähetetään sähköposti palvelun kautta.
        await _emailService.SendEmailAsync(ticket.CreatedByUser.Email, subject, htmlMessage);
    }

    // Rakentaa pudotusvalikon kaikista aktiivisista asiakkaista.
    // Käytetään tiketin luonti- ja muokkauslomakkeissa.
    private async Task<IEnumerable<SelectListItem>> BuildCustomerOptions() =>
        (await _db.Customers
            .Where(c => c.IsActive)         // Vain aktiiviset.
            .OrderBy(c => c.CompanyName)    // Aakkosjärjestyksessä.
            .ToListAsync())
        .Select(c => new SelectListItem(c.CompanyName, c.Id.ToString()));

    // Rakentaa adminin tiketin muokkauslomakkeen ViewModel:in.
    // Sisältää nykyisen tilan, prioriteetin ja listan admin-käyttäjistä vastuuhenkilöksi.
    private async Task<TicketEditViewModel> BuildEditViewModel(Ticket ticket)
    {
        // Haetaan kaikki Admin-roolissa olevat käyttäjät vastuuhenkilöpudotusvalikkoon.
        var staff = await _userManager.GetUsersInRoleAsync("Admin");
        return new TicketEditViewModel
        {
            Id               = ticket.Id,
            Status           = ticket.Status,
            Priority         = ticket.Priority,
            AssignedToUserId = ticket.AssignedToUserId, // Nykyinen vastuuhenkilö (null = ei ketään).
            // Muunnetaan admin-käyttäjät SelectListItem-olioiksi pudotusvalikkoa varten.
            // Näytettävä teksti: koko nimi jos on, muuten sähköposti.
            StaffOptions     = staff.Select(u =>
                new SelectListItem(
                    u.FullName.Length > 0 ? u.FullName : u.Email, // Näytettävä teksti.
                    u.Id))                                          // Arvo (GUID-merkkijono).
        };
    }
}
