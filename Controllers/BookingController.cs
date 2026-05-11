// Nimiavaruuksien tuonnit.
using System.Globalization;                        // CultureInfo — suomenkielinen kuukausiformatointi.
using HakaTech.Portal.Data;                        // ApplicationDbContext — tietokantayhteys.
using HakaTech.Portal.Models.Domain;               // BookingSlot, Booking, BookingStatus, ApplicationUser...
using HakaTech.Portal.Models.ViewModels;           // BookingCalendarViewModel, BookingFormViewModel...
using HakaTech.Portal.Services;                    // IEmailService, IAuditService.
using Microsoft.AspNetCore.Authorization;          // [Authorize]-attribuutti.
using Microsoft.AspNetCore.Identity;               // UserManager.
using Microsoft.AspNetCore.Mvc;                    // Controller, IActionResult, TempData...
using Microsoft.EntityFrameworkCore;               // ToListAsync, Include, AnyAsync, FindAsync...

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Huoltokalenterin controller. Asiakas näkee vapaita aikaikkunoita
/// (BookingSlot) ja voi tehdä varauksen (Booking). Admin luo ja hallinnoi
/// aikaikkunoita sekä vahvistaa varaukset.
/// </summary>
// [Authorize] = koko controller vaatii kirjautumisen.
// Admin- ja Customer-roolit eroavat toisistaan — yksittäisissä
// action-metodeissa tarkistetaan rooli tarkemmin.
[Authorize]
public class BookingController : Controller
{
    // Tietokantayhteys.
    private readonly ApplicationDbContext         _db;
    // UserManager — haetaan kirjautunut käyttäjä.
    private readonly UserManager<ApplicationUser> _userManager;
    // Sähköpostipalvelu — varausvahvistukset ja -ilmoitukset.
    private readonly IEmailService                _emailService;
    // Auditointipalvelu — varauksen luonti, peruutus, vahvistus kirjataan lokiin.
    private readonly IAuditService                _audit;
    // Diagnostiikkaloki kehittäjälle.
    private readonly ILogger<BookingController>   _logger;

    // Suomen kulttuuriasetus — käytetään kuukausien nimien ja päivämäärien
    // tulostamiseen suomeksi (esim. "tammikuu 2025" eikä "January 2025").
    // "static readonly" = luokan tason vakio, luodaan vain kerran koko sovelluksen elinkaaren aikana.
    private static readonly CultureInfo FI = new("fi-FI");

    // Konstruktori: DI-säiliö täyttää kaikki parametrit.
    public BookingController(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager,
        IEmailService                emailService,
        IAuditService                audit,
        ILogger<BookingController>   logger)
    {
        _db           = db;
        _userManager  = userManager;
        _emailService = emailService;
        _audit        = audit;
        _logger       = logger;
    }

    // ── GET /Booking ─────────────────────────────────────────────────────────
    // Kalenterinäkymä: näyttää valitun kuukauden aikaikkunat.
    // Parametrit tulevat URL:sta, esim. /Booking?year=2025&month=3&service=0
    public async Task<IActionResult> Index(
        int? year,               // Näytettävä vuosi. null = kuluva vuosi.
        int? month,              // Näytettävä kuukausi. null = kuluva kuukausi.
        int? day,                // Valittu päivä (korostus kalenterissa). null = ei valittu.
        BookingSlotType? service) // Palvelutyypin suodatin. null = kaikki tyypit.
    {
        // Haetaan kirjautunut käyttäjä — tarvitaan tunnistamaan käyttäjän omat varaukset.
        var currentUser = await _userManager.GetUserAsync(User);
        // Tarkistetaan rooli — admin näkee kaikki aikavälit, asiakas vain aktiiviset.
        bool isAdmin = User.IsInRole("Admin");

        // Haetaan nykyinen paikallinen aika oletusarvoa varten.
        var now = DateTime.Now;
        // Math.Clamp() rajoittaa arvon välille [min, max].
        // Jos year-parametria ei ole annettu, käytetään kuluvaa vuotta.
        // Vuosi rajoitetaan välille 2020–2035 ettei voida kysyä järjettömiä vuosia.
        int y = year .HasValue ? Math.Clamp(year.Value,  2020, 2035) : now.Year;
        // Kuukausi rajoitetaan välille 1–12 (tammikuu–joulukuu).
        int m = month.HasValue ? Math.Clamp(month.Value, 1,    12)   : now.Month;

        // Lasketaan kuukauden alku- ja loppupäivä aikavälikyselyn rajoiksi.
        var monthStart = new DateTime(y, m, 1);           // Esim. 2025-03-01 00:00:00.
        var monthEnd   = monthStart.AddMonths(1);         // Esim. 2025-04-01 00:00:00 (ei-sisältyvä).

        // Haetaan kuukauden kaikki aikavälit tietokannasta.
        // Include().ThenInclude() = kahden tason JOIN:
        //   BookingSlot → Bookings → User  (kuka on varannut)
        //   BookingSlot → Bookings → Customer (minkä yrityksen puolesta)
        var slotsQuery = _db.BookingSlots
            .Include(s => s.Bookings).ThenInclude(b => b.User)
            .Include(s => s.Bookings).ThenInclude(b => b.Customer)
            // Haetaan vain tämän kuukauden aikavälit: alku ≥ kuukauden alku JA alku < seuraavan kuukauden alku.
            .Where(s => s.StartTime >= monthStart && s.StartTime < monthEnd);

        // Asiakas näkee vain aktiiviset (IsActive=true) aikavälit.
        // Admin näkee myös piilotetut aikavälit — hallinnointia varten.
        if (!isAdmin)
            slotsQuery = slotsQuery.Where(s => s.IsActive);

        // Palvelutyypin suodatin — lisätään vain jos parametri on annettu.
        if (service.HasValue)
            slotsQuery = slotsQuery.Where(s => s.SlotType == service.Value);

        // Suoritetaan SQL-kysely ja järjestetään aikavälit alkamisajan mukaan.
        var slots = await slotsQuery.OrderBy(s => s.StartTime).ToListAsync();

        // Haetaan niiden aikaikkunoiden ID:t, jotka kirjautuneella asiakkaalla on jo varattuna.
        // Tätä tarvitaan kalenterissa merkitsemään "jo varattu" -tila.
        // HashSet<int> = joukko kokonaislukuja ilman duplikaatteja, O(1) hakuaika.
        var myBookedSlotIds = new HashSet<int>();
        // Admin ei tarvitse tätä — adminilla ei ole "omia" varauksia.
        if (!isAdmin && currentUser is not null)
        {
            // Haetaan kirjautuneen käyttäjän aktiiviisten (ei peruutettujen) varausten slottiID:t.
            var ids = await _db.Bookings
                .Where(b => b.UserId == currentUser.Id
                         && b.Status != BookingStatus.Cancelled) // Peruutettu = tyhjä paikka.
                .Select(b => b.BookingSlotId)    // Poimitaan vain SlotId-kenttä — ei haeta turhaa dataa.
                .ToListAsync();
            // Muunnetaan lista HashSetiksi nopeaa hakua varten.
            myBookedSlotIds = ids.ToHashSet();
        }

        // Rakennetaan ViewModel — kerää kaiken kalenterinäkymän tarvitseman tiedon.
        var vm = new BookingCalendarViewModel
        {
            Year              = y,
            Month             = m,
            SelectedDay       = day,            // Valittu päivä korostukseen.
            ServiceTypeFilter = service,        // Aktiivinen suodatin (näytetään UI:ssa).
            AllSlots          = slots,          // Kaikki kuukauden aikavälit.
            MyBookedSlotIds   = myBookedSlotIds, // Käyttäjän omat varaukset (HashSet).
            IsAdmin           = isAdmin         // Näkymä muuttuu roolin mukaan.
        };

        return View(vm);
    }

    // ── GET /Booking/Book/5 ──────────────────────────────────────────────────
    // Näyttää yksittäisen aikavälin varauslomakkeen asiakkaalle.
    public async Task<IActionResult> Book(int id)
    {
        // Adminille ei ole varauslomaketta — ohjataan aikavälienhallintaan.
        if (User.IsInRole("Admin"))
            return RedirectToAction(nameof(ManageSlots));

        // Haetaan kirjautunut asiakas.
        var currentUser = await _userManager.GetUserAsync(User);

        // Haetaan aikaikkunan tiedot varauksineen (tarvitaan paikkatilanteen tarkistukseen).
        var slot = await _db.BookingSlots
            .Include(s => s.Bookings) // Lataa olemassaolevat varaukset → IsFull-tarkistus.
            .FirstOrDefaultAsync(s => s.Id == id);

        // Jos aikaväliä ei löydy, palautetaan 404.
        if (slot is null) return NotFound();

        // Tarkistetaan varaustilanne — IsAvailable on laskettu ominaisuus (computed property).
        // Se tarkistaa: IsActive && !IsFull && !IsPast.
        if (!slot.IsAvailable)
        {
            TempData["ErrorMessage"] = "Aikaväli ei ole enää varattavissa.";
            // Ohjataan kalenteriin samalle kuukaudelle.
            return RedirectToAction(nameof(Index),
                new { year = slot.StartTime.Year, month = slot.StartTime.Month });
        }

        // Tarkistetaan ettei käyttäjä ole jo varannut tätä aikaväliä.
        // AnyAsync() = SQL EXISTS — nopeampi kuin lataamalla koko lista.
        bool alreadyBooked = currentUser is not null && await _db.Bookings.AnyAsync(
            b => b.BookingSlotId == id
              && b.UserId == currentUser.Id
              && b.Status != BookingStatus.Cancelled); // Peruutettu ei estä uutta varausta.

        if (alreadyBooked)
        {
            TempData["ErrorMessage"] = "Sinulla on jo varaus tälle aikavälle.";
            return RedirectToAction(nameof(Index),
                new { year = slot.StartTime.Year, month = slot.StartTime.Month });
        }

        // Välitetään aikavälin tiedot näkymälle ViewBagin kautta (ei ViewModel-kenttää tälle).
        ViewBag.Slot = slot;
        // Palautetaan tyhjä varauslomake, johon on esitäytetty aikavälin ID.
        return View(new BookingFormViewModel { BookingSlotId = id });
    }

    // ── POST /Booking/Book ───────────────────────────────────────────────────
    // Käsittelee asiakkaan varauslomakkeen lähetyksen.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookingFormViewModel model)
    {
        // Adminit ohjataan pois — heillä ei ole asiakasvarauksia.
        if (User.IsInRole("Admin"))
            return RedirectToAction(nameof(ManageSlots));

        // Haetaan kirjautunut käyttäjä.
        var currentUser = await _userManager.GetUserAsync(User);
        // currentUser?.CustomerId is null — tarkistetaan että käyttäjällä on yritys.
        // Jos CustomerId puuttuu (admin tai rekisteröimätön), kielletään pääsy.
        // Forbid() = HTTP 403 Forbidden.
        if (currentUser?.CustomerId is null) return Forbid();

        // Haetaan varattava aikaväli varauksineen (paikkatilanteen tarkistus).
        var slot = await _db.BookingSlots
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.Id == model.BookingSlotId);

        if (slot is null) return NotFound();

        // Tarkistetaan lomakkeen validointi (mm. pakolliset kentät).
        // Jos epäkelpo, palautetaan lomake aikavälin tiedoilla täytettynä.
        if (!ModelState.IsValid)
        {
            ViewBag.Slot = slot;
            return View(model);
        }

        // Aikavälin saatavuuden toisintarkistus (voi olla muuttunut GET:n jälkeen).
        // Kilpailutilanne: toinen käyttäjä saattoi varata viimeisen paikan välissä.
        if (!slot.IsAvailable)
        {
            TempData["ErrorMessage"] = "Aikaväli ei ole enää varattavissa.";
            return RedirectToAction(nameof(Index),
                new { year = slot.StartTime.Year, month = slot.StartTime.Month });
        }

        // Toisintarkistus: onko käyttäjä jo varannut (POST-lomake voidaan lähettää kahdesti).
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

        // Luodaan uusi Booking-entiteetti.
        var booking = new Booking
        {
            BookingSlotId = model.BookingSlotId,
            // .Value — CustomerId on nullable int?, tässä vaiheessa tiedetään se ei ole null (tarkistettiin yllä).
            CustomerId    = currentUser.CustomerId.Value,
            UserId        = currentUser.Id,
            // Trim() poistaa ylimääräiset välilyönnit muistiinpanon alusta/lopusta.
            // "?" = jos Notes on null, Trim()-kutsu jätetään tekemättä (ei kaadu null-virheen).
            Notes         = model.Notes?.Trim(),
            Status        = BookingStatus.Pending,  // Alkutila: odottaa adminin vahvistusta.
            CreatedAt     = DateTime.UtcNow          // Tallennusaika UTC-muodossa.
        };

        // Tallennetaan varaus tietokantaan.
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        // Kirjataan varaus auditlokiin turvallisuusseurantaa varten.
        // Parametrit: toiminto, kohdetyyppi, kohteen ID, lisätiedot (teksti).
        await _audit.LogAsync("BookingCreated", "Booking", booking.Id.ToString(),
            $"{slot.Title} / {slot.StartTime:dd.MM.yyyy HH:mm}");

        // Lähetetään vahvistussähköposti asiakkaalle.
        // try-catch: sähköpostin lähetys ei saa kaataa koko varauksen tallennusta.
        try
        {
            // Haetaan asiakasyrityksen nimi sähköpostipohjaa varten.
            var customer = await _db.Customers.FindAsync(currentUser.CustomerId.Value);
            // Tarkistetaan että sähköpostiosoite on olemassa ennen lähetystä.
            if (!string.IsNullOrWhiteSpace(currentUser.Email))
            {
                await _emailService.SendEmailAsync(
                    currentUser.Email,                         // Vastaanottajan osoite.
                    $"Varausvahvistus – {slot.Title}",         // Aihe.
                    BookingRequestEmail(                        // HTML-runko (yksityinen apumetodi).
                        slot,
                        // "??" = null-yhdistämisoperaattori: jos FullName on null, käytetään sähköpostiosoitetta.
                        currentUser.FullName ?? currentUser.Email,
                        // Sama: jos yritystä ei löydy, käytetään tyhjää merkkijonoa.
                        customer?.CompanyName ?? ""));
            }
        }
        catch (Exception ex)
        {
            // Kirjataan virhe lokiin mutta ei estetä varauksen onnistumista.
            // LogWarning = varoitus (ei kriittinen virhe, ohjelma jatkaa toimintaansa).
            _logger.LogWarning(ex, "Varausvahvistuksen lähetys epäonnistui.");
        }

        // Onnistumisviesti joka näytetään seuraavalla sivulla (TempData säilyy yhden uudelleenohauksen ajan).
        TempData["SuccessMessage"] =
            $"Varaus vastaanotettu: {slot.Title} {slot.StartTime:dd.MM.yyyy HH:mm}. " +
            "Saat vahvistuksen sähköpostiin.";
        // Ohjataan käyttäjä omien varaustensa listalle.
        return RedirectToAction(nameof(MyBookings));
    }

    // ── POST /Booking/Cancel/5 ───────────────────────────────────────────────
    // Peruuttaa varauksen. Sekä asiakas (omansa) että admin (kenen tahansa) voi peruuttaa.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(
        int id,          // Peruutettavan varauksen ID.
        string? reason)  // Peruutuksen syy (vapaaehtoinen).
    {
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin    = User.IsInRole("Admin");

        // Haetaan varaus aikavälin tiedoilla (näytetään vahvistusviestissä).
        var booking = await _db.Bookings
            .Include(b => b.BookingSlot)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null) return NotFound();

        // Turvallisuustarkistus: asiakas saa peruuttaa vain oman varauksensa.
        // Admin saa peruuttaa kenen tahansa varauksen.
        // "!" = looginen EI. Ehto: ei ole admin JA varauksen käyttäjä ei ole kirjautunut käyttäjä.
        if (!isAdmin && booking.UserId != currentUser?.Id)
            return Forbid(); // HTTP 403 — sinulla ei ole oikeutta tähän varaukseen.

        // Tarkistetaan ettei varausta ole jo peruutettu.
        if (booking.Status == BookingStatus.Cancelled)
        {
            TempData["ErrorMessage"] = "Varaus on jo peruutettu.";
            // Ohjataan oikeaan listaan roolin mukaan.
            return isAdmin
                ? RedirectToAction(nameof(ManageBookings))  // Admin → hallintasivu.
                : RedirectToAction(nameof(MyBookings));      // Asiakas → omat varaukset.
        }

        // Päivitetään varauksen tila peruutetuksi.
        booking.Status             = BookingStatus.Cancelled;
        booking.CancelledAt        = DateTime.UtcNow;          // Peruutusaika UTC:nä.
        booking.CancellationReason = reason?.Trim();            // Syy (voi olla null).
        await _db.SaveChangesAsync();

        // Auditloki — "?" = jos BookingSlot on null (puuttuu), ei kaadu.
        await _audit.LogAsync("BookingCancelled", "Booking", id.ToString(),
            booking.BookingSlot?.Title);

        TempData["SuccessMessage"] = "Varaus peruutettu.";
        // Ohjataan oikealle sivulle roolin mukaan.
        return isAdmin
            ? RedirectToAction(nameof(ManageBookings))
            : RedirectToAction(nameof(MyBookings));
    }

    // ── GET /Booking/MyBookings ──────────────────────────────────────────────
    // Asiakkaan omat varaukset: tulevat (Upcoming) ja menneet/peruutetut (Past).
    public async Task<IActionResult> MyBookings()
    {
        // Admin ohjataan hallintatyökaluun.
        if (User.IsInRole("Admin"))
            return RedirectToAction(nameof(ManageBookings));

        var currentUser = await _userManager.GetUserAsync(User);
        // Unauthorized() = HTTP 401 — ei kirjautunut (ei pitäisi tapahtua [Authorize]-attribuutin takia).
        if (currentUser is null) return Unauthorized();

        // Haetaan nykyinen aika vertailua varten (tulevat vs. menneet).
        var now = DateTime.Now;

        // Haetaan kaikki käyttäjän varaukset aikavälin tiedoilla.
        var bookings = await _db.Bookings
            .Include(b => b.BookingSlot)         // Tarvitaan StartTime-vertailuun ja näyttöön.
            .Where(b => b.UserId == currentUser.Id) // Vain oman käyttäjän varaukset.
            .OrderByDescending(b => b.BookingSlot!.StartTime) // Uusin ensin.
            // "!" = null-forgiving: kehittäjä lupaa BookingSlot ei ole null (Include lataa sen).
            .ToListAsync();

        // Jaetaan varaukset kahteen ryhmään ViewModel:iin.
        var vm = new BookingMyViewModel
        {
            // Tulevat: ei peruutettu JA alkaa tulevaisuudessa.
            Upcoming = bookings
                .Where(b => b.Status != BookingStatus.Cancelled
                         && b.BookingSlot?.StartTime >= now)
                .ToList(),
            // Menneet: peruutettu TAI aikaväli on jo menneisyydessä.
            Past = bookings
                .Where(b => b.Status == BookingStatus.Cancelled
                         || b.BookingSlot?.StartTime < now)
                .ToList()
        };

        return View(vm);
    }

    // ════════════════════════════════════════════════════════════════════════
    // ADMIN-TOIMINNOT
    // ════════════════════════════════════════════════════════════════════════

    // ── GET /Booking/ManageSlots ─────────────────────────────────────────────
    // Admin näkee kuukauden kaikki aikavälit (myös piilotetut) hallintanäkymässä.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ManageSlots(int? year, int? month)
    {
        // Sama vuosi/kuukausi-logiikka kuin Index-metodissa.
        var now = DateTime.Now;
        int y   = year .HasValue ? Math.Clamp(year.Value,  2020, 2035) : now.Year;
        int m   = month.HasValue ? Math.Clamp(month.Value, 1,    12)   : now.Month;

        var monthStart = new DateTime(y, m, 1);
        var monthEnd   = monthStart.AddMonths(1);

        // Haetaan kaikki kuukauden aikavälit varauksineen ja varaajineen.
        // Admin tarvitsee tiedon: kuka on varannut, kuinka monta paikkaa jäljellä.
        var slots = await _db.BookingSlots
            .Include(s => s.Bookings).ThenInclude(b => b.User)
            .Where(s => s.StartTime >= monthStart && s.StartTime < monthEnd)
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        // Lasketaan edellinen ja seuraava kuukausi kalenterin navigointipainikkeita varten.
        // Tuplen purkaminen: (py, pm) = edellinen vuosi ja kuukausi.
        // Tammikuussa (m==1) taaksepäin mennään joulukuuhun (12) ja edelliseen vuoteen (y-1).
        var (py, pm) = m == 1  ? (y - 1, 12) : (y, m - 1);
        // Joulukuussa (m==12) eteenpäin mennään tammikuuhun (1) ja seuraavaan vuoteen (y+1).
        var (ny, nm) = m == 12 ? (y + 1,  1) : (y, m + 1);

        // Välitetään tiedot näkymälle ViewBagin kautta.
        ViewBag.Year      = y;
        ViewBag.Month     = m;
        // ToString("MMMM yyyy", FI) = suomenkielinen kuukausinimi + vuosi, esim. "maaliskuu 2025".
        ViewBag.MonthName = monthStart.ToString("MMMM yyyy", FI);
        ViewBag.PrevYear  = py; ViewBag.PrevMonth = pm;   // Edellisen kuukauden navigointiparametrit.
        ViewBag.NextYear  = ny; ViewBag.NextMonth = nm;   // Seuraavan kuukauden navigointiparametrit.

        // Lähetetään aikavälit lista-näkymälle.
        return View(slots);
    }

    // ── GET /Booking/CreateSlot ──────────────────────────────────────────────
    // Näyttää tyhjän aikaikunan luontilomakkeen.
    [Authorize(Roles = "Admin")]
    public IActionResult CreateSlot(DateTime? date)
    {
        // Oletusaloitusaika: annettu päivä klo 09:00, tai huomenna klo 09:00.
        // "?" = jos date on null, käytetään oikealla puolen olevaa arvoa.
        // .Date = poistaa kellonajan (palauttaa 00:00:00), AddHours(9) asettaa klo 09:00.
        var def = date?.Date.AddHours(9) ?? DateTime.Now.Date.AddDays(1).AddHours(9);
        // FormAction ohjaa näkymässä, mihin URL:iin lomake lähetetään (Create tai Edit käyttävät samaa näkymää).
        ViewBag.FormAction = "CreateSlot";
        // Käytetään yhteistä "SlotForm"-näkymää sekä luomiseen että muokkaamiseen.
        return View("SlotForm", new BookingSlotFormViewModel { StartTime = def });
    }

    // ── POST /Booking/CreateSlot ─────────────────────────────────────────────
    // Tallentaa uuden aikavälin tietokantaan.
    // [HttpPost] = käsittelee vain POST-pyyntöjä.
    // [ValidateAntiForgeryToken] = CSRF-suoja.
    // [Authorize(Roles = "Admin")] = vain adminit.
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateSlot(BookingSlotFormViewModel model)
    {
        // Tarkistetaan lomakkeen validointi.
        if (!ModelState.IsValid)
        {
            ViewBag.FormAction = "CreateSlot";
            return View("SlotForm", model); // Palautetaan sama näkymä virheineen.
        }

        // Haetaan luova admin auditlokia varten.
        var currentUser = await _userManager.GetUserAsync(User);

        // Luodaan uusi BookingSlot-entiteetti ViewModelista.
        var slot = new BookingSlot
        {
            Title           = model.Title.Trim(),            // Trim() poistaa ylimääräiset välilyönnit.
            Description     = model.Description?.Trim(),     // Vapaaehtoinen kuvaus.
            SlotType        = model.SlotType,                // Palvelutyyppi (enum).
            StartTime       = model.StartTime,               // Alkamisaika (paikallinen aika).
            DurationMinutes = model.DurationMinutes,         // Kesto minuutteina.
            MaxCapacity     = model.MaxCapacity,             // Enimmäisvarausmäärä.
            IsActive        = model.IsActive,                // Näkyykö asiakkaille.
            // Luojan käyttäjätunnus — "?" jos currentUser olisi null (ei pitäisi tapahtua adminille).
            CreatedByUserId = currentUser?.Id
        };

        _db.BookingSlots.Add(slot);
        await _db.SaveChangesAsync(); // INSERT SQL.

        // Kirjataan aikavälin luonti auditlokiin.
        await _audit.LogAsync("SlotCreated", "BookingSlot", slot.Id.ToString(), slot.Title);

        TempData["SuccessMessage"] = $"Aikaväli '{slot.Title}' luotu.";
        // Ohjataan kalenteriin luodun aikavälin kuukaudelle.
        return RedirectToAction(nameof(Index),
            new { year = slot.StartTime.Year, month = slot.StartTime.Month });
    }

    // ── GET /Booking/EditSlot/5 ──────────────────────────────────────────────
    // Muokkauslomake olemassaolevalle aikavälille.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditSlot(int id)
    {
        // Haetaan aikaväli pääavaimella — nopein tapa yksittäiselle riville.
        var slot = await _db.BookingSlots.FindAsync(id);
        if (slot is null) return NotFound();

        // Käytetään samaa "SlotForm"-näkymää kuin luomisessa.
        ViewBag.FormAction = "EditSlot";
        // Täytetään ViewModel olemassaolevasta entiteetistä.
        return View("SlotForm", new BookingSlotFormViewModel
        {
            Id              = slot.Id,          // Tarvitaan tunnistamaan muokattava rivi POST:ssa.
            Title           = slot.Title,
            Description     = slot.Description,
            SlotType        = slot.SlotType,
            StartTime       = slot.StartTime,
            DurationMinutes = slot.DurationMinutes,
            MaxCapacity     = slot.MaxCapacity,
            IsActive        = slot.IsActive
        });
    }

    // ── POST /Booking/EditSlot ───────────────────────────────────────────────
    // Tallentaa muokatun aikavälin tietokantaan.
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditSlot(BookingSlotFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.FormAction = "EditSlot";
            return View("SlotForm", model);
        }

        // Haetaan muokattava entiteetti ID:n perusteella.
        var slot = await _db.BookingSlots.FindAsync(model.Id);
        if (slot is null) return NotFound();

        // Päivitetään kentät yksi kerrallaan ViewModelista entiteettiin.
        // EF Core seuraa muutoksia automaattisesti (change tracking) — SaveChangesAsync() generoi UPDATE SQL:n.
        slot.Title           = model.Title.Trim();
        slot.Description     = model.Description?.Trim();
        slot.SlotType        = model.SlotType;
        slot.StartTime       = model.StartTime;
        slot.DurationMinutes = model.DurationMinutes;
        slot.MaxCapacity     = model.MaxCapacity;
        slot.IsActive        = model.IsActive;

        await _db.SaveChangesAsync(); // UPDATE SQL.
        await _audit.LogAsync("SlotUpdated", "BookingSlot", slot.Id.ToString(), slot.Title);

        TempData["SuccessMessage"] = $"Aikaväli '{slot.Title}' päivitetty.";
        return RedirectToAction(nameof(Index),
            new { year = slot.StartTime.Year, month = slot.StartTime.Month });
    }

    // ── POST /Booking/DeleteSlot/5 ───────────────────────────────────────────
    // Poistaa aikavälin — mutta vain jos sillä ei ole aktiivisia varauksia.
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSlot(int id)
    {
        // Ladataan aikaväli varauksineen — tarvitaan aktiivisuustarkistukseen.
        var slot = await _db.BookingSlots
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (slot is null) return NotFound();

        // Turvallisuustarkistus: aikaväliä ei voi poistaa jos sillä on
        // aktiivisia (ei-peruutettuja) varauksia.
        // Any() palauttaa true heti kun yksikin vastaava rivi löytyy.
        if (slot.Bookings.Any(b => b.Status != BookingStatus.Cancelled))
        {
            TempData["ErrorMessage"] =
                "Aikaväliä ei voi poistaa — sillä on aktiivisia varauksia.";
            return RedirectToAction(nameof(Index),
                new { year = slot.StartTime.Year, month = slot.StartTime.Month });
        }

        // Poistetaan aikaväli (ja sen peruutetut varaukset cascade-deleten ansiosta).
        _db.BookingSlots.Remove(slot);
        await _db.SaveChangesAsync(); // DELETE SQL.
        await _audit.LogAsync("SlotDeleted", "BookingSlot", id.ToString(), slot.Title);

        TempData["SuccessMessage"] = $"Aikaväli '{slot.Title}' poistettu.";
        // Ohjataan takaisin kalenterin pääsivulle.
        return RedirectToAction(nameof(Index));
    }

    // ── GET /Booking/ManageBookings ──────────────────────────────────────────
    // Admin näkee kaikki varaukset suodatusmahdollisuuksilla.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ManageBookings(
        BookingStatus? status,    // Suodatin: vain tietyssä tilassa olevat. null = kaikki.
        int? customerId)          // Suodatin: vain tietyn asiakkaan varaukset. null = kaikki.
    {
        // Aloitetaan kyselyobjektilla jolle lisätään ehtoja alla.
        var query = _db.Bookings
            .Include(b => b.BookingSlot)  // Aikavälin tiedot (otsikko, ajankohta).
            .Include(b => b.User)          // Varaajan nimi ja sähköposti.
            .Include(b => b.Customer)      // Asiakasyrityksen nimi.
            .AsQueryable();

        // Tilasuodatin — lisätään kyselyyn vain jos annettu.
        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        // Asiakassuodatin — lisätään kyselyyn vain jos annettu.
        if (customerId.HasValue)
            query = query.Where(b => b.CustomerId == customerId.Value);

        // Haetaan tulokset uusimmasta aikaikkunasta vanhimpaan.
        // "!" = null-forgiving: kehittäjä tietää BookingSlot ei ole null (Include lataa sen).
        var bookings = await query
            .OrderByDescending(b => b.BookingSlot!.StartTime)
            .ToListAsync();

        // Välitetään suodattimet näkymälle (lomake pysyy täytettynä).
        ViewBag.StatusFilter   = status;
        ViewBag.CustomerFilter = customerId;
        // Asiakaslistaa pudotusvalikkoa varten — vain aktiiviset yritykset aakkosjärjestyksessä.
        ViewBag.Customers = await _db.Customers
            .Where(c => c.IsActive).OrderBy(c => c.CompanyName).ToListAsync();

        return View(bookings);
    }

    // ── POST /Booking/ConfirmBooking/5 ───────────────────────────────────────
    // Admin vahvistaa yksittäisen varauksen ja lähettää vahvistussähköpostin asiakkaalle.
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> ConfirmBooking(int id)
    {
        // Haetaan varaus aikavälin ja käyttäjän tiedoilla sähköpostin lähettämistä varten.
        var booking = await _db.Bookings
            .Include(b => b.BookingSlot)  // Aikavälin otsikko ja aika sähköpostiin.
            .Include(b => b.User)          // Asiakkaan sähköpostiosoite.
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null) return NotFound();

        // Muutetaan tila Pending → Confirmed.
        booking.Status = BookingStatus.Confirmed;
        await _db.SaveChangesAsync(); // UPDATE SQL.
        await _audit.LogAsync("BookingConfirmed", "Booking", id.ToString(),
            booking.BookingSlot?.Title);

        // Lähetetään vahvistussähköposti asiakkaalle.
        try
        {
            // Tarkistetaan että asiakkaalla on sähköpostiosoite.
            if (!string.IsNullOrWhiteSpace(booking.User?.Email))
            {
                await _emailService.SendEmailAsync(
                    booking.User.Email,
                    $"Varauksesi vahvistettu – {booking.BookingSlot?.Title}",
                    BookingConfirmedEmail(
                        booking.BookingSlot!,                           // Aikavälin tiedot.
                        booking.User.FullName ?? booking.User.Email));  // Asiakkaan nimi tai sähköposti.
            }
        }
        catch (Exception ex)
        {
            // Sähköpostin lähetysvirhe kirjataan lokiin mutta ei kaada vahvistustoimintoa.
            _logger.LogWarning(ex, "Vahvistussähköpostin lähetys epäonnistui.");
        }

        TempData["SuccessMessage"] = "Varaus vahvistettu ja asiakkaalle lähetetty tieto.";
        return RedirectToAction(nameof(ManageBookings));
    }

    // ── Sähköpostipohjat ─────────────────────────────────────────────────────
    // Yksityiset apumetodit HTML-sähköpostipohjien rakentamiseen.
    // "private static" = ei tarvita instanssia (ei _db tai _emailService), pelkkä merkkijonon palautus.

    // Varauspyynön vastaanottosähköposti — lähetetään kun asiakas tekee varauksen.
    // Parametrit: aikavälin tiedot, asiakkaan nimi, yrityksen nimi.
    // "=>" ja $""" ... """ = C# 11:n raw string literal — pitkä merkkijono ilman paljon escaping-merkkejä.
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
                  <td style="padding:8px 0;font-weight:600">
                    {slot.StartTime:dd.MM.yyyy HH:mm} – {slot.EndTime:HH:mm}</td></tr>
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

    // Vahvistetun varauksen sähköposti — lähetetään kun admin vahvistaa varauksen.
    // Vihreä otsikkorivi (#16a34a) erottaa tämän vastaanottovahvistuksesta.
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
                  <td style="padding:8px 0;font-weight:600;color:#16a34a">
                    {slot.StartTime:dd.MM.yyyy HH:mm} – {slot.EndTime:HH:mm}</td></tr>
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
