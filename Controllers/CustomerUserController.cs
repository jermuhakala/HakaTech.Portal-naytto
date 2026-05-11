// Nimiavaruuksien tuonnit.
using HakaTech.Portal.Data;                        // ApplicationDbContext — tietokantayhteys.
using HakaTech.Portal.Models.Domain;               // ApplicationUser — käyttäjäentiteetti.
using HakaTech.Portal.Models.ViewModels;           // CustomerUserFormViewModel, CustomerUserEditViewModel.
using HakaTech.Portal.Services;                    // IAuditService — käyttäjätoimet lokitetaan.
using Microsoft.AspNetCore.Authorization;          // [Authorize]-attribuutti.
using Microsoft.AspNetCore.Identity;               // UserManager — käyttäjien hallinta.
using Microsoft.AspNetCore.Mvc;                    // Controller, IActionResult, TempData...
using Microsoft.EntityFrameworkCore;               // ToListAsync.

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Asiakasyrityksen pääkäyttäjä hallitsee oman yrityksensä käyttäjiä.
/// Admin pääsee kaikille yrityksille.
/// </summary>
// [Authorize] = kirjautuminen vaaditaan.
// Tässä controllerissa on kaksitasoinen pääsyhierarkia:
//   1. Admin: pääsee kaikille yrityksille.
//   2. CustomerAdmin: pääsee vain omaan yritykseen (IsCustomerAdmin=true).
//   3. Tavallinen asiakas: kielletty pääsy.
[Authorize]
public class CustomerUserController : Controller
{
    // Tietokantayhteys.
    private readonly ApplicationDbContext         _db;
    // UserManager — ASP.NET Identity käyttäjien hallinta (luonti, poisto, roolit).
    private readonly UserManager<ApplicationUser> _userManager;
    // Auditointipalvelu — käyttäjien luonti, muokkaus ja poisto kirjataan lokiin.
    private readonly IAuditService                _audit;

    // Konstruktori: DI-säiliö täyttää parametrit.
    public CustomerUserController(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager,
        IAuditService                audit)
    {
        _db          = db;
        _userManager = userManager;
        _audit       = audit;
    }

    // ── GET /CustomerUser  tai  /CustomerUser?customerId=5 ──────────────────
    // Näyttää yrityksen käyttäjälistan.
    // Admin voi valita yrityksen URL-parametrilla, CustomerAdmin näkee vain omansa.
    public async Task<IActionResult> Index(
        int? customerId) // Valinnainen: minkä yrityksen käyttäjät. null = oma yritys.
    {
        var me      = await _userManager.GetUserAsync(User);
        bool isAdmin = User.IsInRole("Admin");

        // Selvitetään minkä yrityksen käyttäjät näytetään.
        // Admin: URL-parametri tai oma yritys (jos admin on myös asiakaskäyttäjä).
        // Asiakas: aina oma yritys — ei voi valita toista.
        // "??" = null-yhdistämisoperaattori: jos customerId on null, käytetään me?.CustomerId.
        int? targetCustomerId = isAdmin ? (customerId ?? me?.CustomerId) : me?.CustomerId;

        // Asiakkaan täytyy olla CustomerAdmin (pääkäyttäjä) — tavallinen asiakas ei pääse.
        // "!= true" vertaa nullable bool:iin: null != true on tosi.
        if (!isAdmin && me?.IsCustomerAdmin != true)
            return Forbid(); // HTTP 403.

        // Jos yritysnumeroa ei ole määritetty, ohjataan admin valitsemaan asiakas.
        if (targetCustomerId is null)
            return isAdmin
                ? RedirectToAction("Index", "Customer") // Admin ohjataan asiakaslistaan.
                : Forbid(); // Asiakkaalla täytyy aina olla yritys.

        // Haetaan yrityksen perustiedot.
        var customer = await _db.Customers.FindAsync(targetCustomerId.Value);
        if (customer is null) return NotFound();

        // Haetaan yrityksen kaikki käyttäjät — suodatetaan CustomerId:n perusteella.
        // _db.Users = ASP.NET Identity users -taulu (AspNetUsers).
        var users = await _db.Users
            .Where(u => u.CustomerId == targetCustomerId.Value)
            .OrderBy(u => u.FullName) // Aakkosjärjestys koko nimen mukaan.
            .ToListAsync();

        // Välitetään yrityksen tiedot näkymälle (otsikkoon).
        ViewBag.Customer = customer;
        ViewBag.IsAdmin  = isAdmin;
        return View(users);
    }

    // ── GET /CustomerUser/Create?customerId=5 ───────────────────────────────
    // Tyhjä uuden käyttäjän luontilomake.
    public async Task<IActionResult> Create(int customerId)
    {
        var me      = await _userManager.GetUserAsync(User);
        bool isAdmin = User.IsInRole("Admin");

        // Turvallisuustarkistus: CustomerAdmin saa luoda käyttäjiä vain omaan yritykseen.
        // me.CustomerId != customerId = yrittää luoda käyttäjää toiseen yritykseen → kielletty.
        if (!isAdmin && (me?.IsCustomerAdmin != true || me.CustomerId != customerId))
            return Forbid();

        // Haetaan yritys otsikkoa varten.
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer is null) return NotFound();

        ViewBag.Customer = customer;
        // Esitäytetään customerId lomakkeeseen piilokentäksi.
        return View(new CustomerUserFormViewModel { CustomerId = customerId });
    }

    // ── POST /CustomerUser/Create ────────────────────────────────────────────
    // Luo uuden käyttäjätilin asiakasyritykseen.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerUserFormViewModel model)
    {
        var me      = await _userManager.GetUserAsync(User);
        bool isAdmin = User.IsInRole("Admin");

        // Toisintarkistus: CustomerId ei saa olla muutettu lomakkeesta (piilokenttä).
        // Tämä estää IDOR:n: CustomerAdmin ei voi luoda käyttäjää toisen yrityksen nimissä
        // vaihtamalla lomakkeen piilokenttää.
        if (!isAdmin && (me?.IsCustomerAdmin != true || me.CustomerId != model.CustomerId))
            return Forbid();

        var customer = await _db.Customers.FindAsync(model.CustomerId);
        if (customer is null) return NotFound();

        // Validoidaan lomake (sähköposti, salasana, nimi).
        if (!ModelState.IsValid)
        {
            ViewBag.Customer = customer;
            return View(model);
        }

        // Luodaan uusi ApplicationUser-olio.
        var user = new ApplicationUser
        {
            // UserName = sähköpostiosoite — ASP.NET Identity käyttää tätä kirjautumiseen.
            UserName        = model.Email,
            Email           = model.Email,
            FullName        = model.FullName,
            CustomerId      = model.CustomerId,      // Yhdistetään yritykseen.
            IsCustomerAdmin = model.IsCustomerAdmin, // Onko pääkäyttäjä.
            // EmailConfirmed = true: ei tarvita sähköpostivahvistusta — admin luo tilin.
            EmailConfirmed  = true
        };

        // CreateAsync salaa salasanan automaattisesti ja tallentaa käyttäjän Identity-tauluihin.
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            // Lisätään kaikki Identity-virheet (esim. "salasana liian lyhyt") lomakkeelle.
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            ViewBag.Customer = customer;
            return View(model);
        }

        // Lisätään käyttäjä Customer-rooliin — tämä määrää mitä käyttäjä näkee portaalissa.
        await _userManager.AddToRoleAsync(user, "Customer");
        // Kirjataan käyttäjän luonti auditlokiin turvallisuusseurantaa varten.
        await _audit.LogAsync("UserCreated", "User", user.Id,
            $"{model.Email} yritykselle {customer.CompanyName}");

        TempData["SuccessMessage"] = $"Käyttäjä {model.Email} luotu.";
        // Ohjataan yrityksen käyttäjälistaan.
        return RedirectToAction(nameof(Index), new { customerId = model.CustomerId });
    }

    // ── GET /CustomerUser/Edit/userId ────────────────────────────────────────
    // Muokkauslomake olemassaolevalle käyttäjälle.
    // Parametri on string (ei int) koska ASP.NET Identity käyttää GUID-merkkijonoja ID:nä.
    public async Task<IActionResult> Edit(string id)
    {
        var me      = await _userManager.GetUserAsync(User);
        bool isAdmin = User.IsInRole("Admin");

        // FindByIdAsync = haku GUID-merkkijonon perusteella.
        var target = await _userManager.FindByIdAsync(id);
        if (target is null) return NotFound();

        // Turvallisuus: CustomerAdmin saa muokata vain oman yrityksensä käyttäjiä.
        if (!isAdmin && (me?.IsCustomerAdmin != true || me.CustomerId != target.CustomerId))
            return Forbid();
        // CustomerAdmin ei voi muokata omaa tiliään tällä toiminnolla.
        // Estää itsetuhon: ei voi ottaa itseltään CustomerAdmin-oikeutta.
        if (target.Id == me?.Id && !isAdmin)
            return Forbid();

        ViewBag.IsAdmin = isAdmin;
        // Täytetään ViewModel muokattavasta käyttäjästä.
        return View(new CustomerUserEditViewModel
        {
            UserId          = target.Id,
            FullName        = target.FullName,
            Email           = target.Email ?? "",     // null → tyhjä (ei pitäisi olla null).
            IsCustomerAdmin = target.IsCustomerAdmin,
            // CustomerId on nullable int? — "?? 0" = jos null, käytetään 0 (ei pitäisi tapahtua).
            CustomerId      = target.CustomerId ?? 0
        });
    }

    // ── POST /CustomerUser/Edit ──────────────────────────────────────────────
    // Tallentaa muokatun käyttäjän tiedot.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CustomerUserEditViewModel model)
    {
        var me      = await _userManager.GetUserAsync(User);
        bool isAdmin = User.IsInRole("Admin");

        // Haetaan muokattava käyttäjä ID:n perusteella.
        var target = await _userManager.FindByIdAsync(model.UserId);
        if (target is null) return NotFound();

        // Turvallisuustarkistus: CustomerAdmin saa muokata vain oman yrityksensä käyttäjiä.
        if (!isAdmin && (me?.IsCustomerAdmin != true || me.CustomerId != target.CustomerId))
            return Forbid();

        if (!ModelState.IsValid)
        {
            ViewBag.IsAdmin = isAdmin;
            return View(model);
        }

        // Päivitetään muuttuneet tiedot.
        target.FullName        = model.FullName;
        target.IsCustomerAdmin = model.IsCustomerAdmin; // Voi ottaa/antaa pääkäyttäjäoikeuden.

        // Sähköpostiosoitteen vaihto — vain jos se on muuttunut.
        // StringComparison.OrdinalIgnoreCase = kirjainkoko ei vaikuta vertailuun.
        if (!string.Equals(target.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            // UserName = sähköposti — Identity käyttää molempia kirjautumiseen.
            target.UserName = model.Email;
            target.Email    = model.Email;
        }

        // UpdateAsync tallentaa muutokset ASP.NET Identity -tauluihin.
        await _userManager.UpdateAsync(target);
        // Kirjataan muutos auditlokiin.
        await _audit.LogAsync("UserUpdated", "User", target.Id, $"Muokkasi: {target.Email}");

        TempData["SuccessMessage"] = $"Käyttäjä {target.Email} päivitetty.";
        // Ohjataan yrityksen käyttäjälistaan.
        return RedirectToAction(nameof(Index), new { customerId = target.CustomerId });
    }

    // ── POST /CustomerUser/Delete ────────────────────────────────────────────
    // Poistaa käyttäjätilin pysyvästi (ei pehmeä poisto — poistetaan Identity-taulusta).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var me      = await _userManager.GetUserAsync(User);
        bool isAdmin = User.IsInRole("Admin");

        var target = await _userManager.FindByIdAsync(id);
        if (target is null) return NotFound();

        // Turvallisuustarkistus: CustomerAdmin saa poistaa vain oman yrityksensä käyttäjiä.
        if (!isAdmin && (me?.IsCustomerAdmin != true || me.CustomerId != target.CustomerId))
            return Forbid();

        // Itsetuhon estäminen: kukaan ei saa poistaa omaa tiliään tällä toiminnolla.
        // (Estää lukituksen: ei voi jäädä yritykseen ilman yhtään käyttäjää.)
        if (target.Id == me?.Id)
        {
            TempData["ErrorMessage"] = "Et voi poistaa omaa käyttäjätiliäsi.";
            return RedirectToAction(nameof(Index), new { customerId = target.CustomerId });
        }

        // Tallennetaan tiedot ennen poistoa — DeleteAsync poistaa olion tietokannasta.
        int? cid     = target.CustomerId;       // Yrityksen ID ohjausta varten.
        string email = target.Email ?? target.Id; // Sähköposti lokia varten (null → ID).

        // Poistetaan käyttäjätili ASP.NET Identity -tauluista (käyttäjä, roolit, väittämät).
        await _userManager.DeleteAsync(target);
        // Kirjataan poisto auditlokiin — tärkeä turvallisuusjäljitettävyyden kannalta.
        await _audit.LogAsync("UserDeleted", "User", id, $"Poistettu: {email}");

        TempData["SuccessMessage"] = $"Käyttäjä {email} poistettu.";
        // Ohjataan yrityksen käyttäjälistaan (poistettu käyttäjä ei enää ole siellä).
        return RedirectToAction(nameof(Index), new { customerId = cid });
    }
}
