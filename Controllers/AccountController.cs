// Tuodaan tarvittavat nimiavaruudet käyttöön.
// Data = tietokannan konteksti (ApplicationDbContext).
using HakaTech.Portal.Data;
// Domain-mallit — ApplicationUser.
using HakaTech.Portal.Models.Domain;
// ViewModelit — LoginViewModel, RegisterViewModel jne.
using HakaTech.Portal.Models.ViewModels;
// IAuditService — audit-lokitusrajapinta.
using HakaTech.Portal.Services;
// [Authorize], [AllowAnonymous] — käyttöoikeuksien rajoittamiseen.
using Microsoft.AspNetCore.Authorization;
// UserManager ja SignInManager — ASP.NET Identityn käyttäjähallinta.
using Microsoft.AspNetCore.Identity;
// IRequestCultureFeature, CookieRequestCultureProvider — kielen valinta.
using Microsoft.AspNetCore.Localization;
// Controller-luokka, IActionResult, RedirectToAction jne.
using Microsoft.AspNetCore.Mvc;
// SelectListItem — pudotusvalikot.
using Microsoft.AspNetCore.Mvc.Rendering;
// [EnableRateLimiting] — kirjautumisen nopeusrajoitus.
using Microsoft.AspNetCore.RateLimiting;
// EF Core — .Where(), .ToListAsync() jne.
using Microsoft.EntityFrameworkCore;

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Käyttäjätilien controller: kirjautuminen, uloskirjautuminen, rekisteröinti
/// (vain admin), salasanan vaihto, kielen valinta ja käyttöoikeuden eston sivu.
/// Tärkeää: kirjautumissivulla on rate-limit-suoja brute-forcea vastaan.
/// </summary>
// Controller = luokka, joka vastaanottaa HTTP-pyyntöjä ja palauttaa vastauksia.
// Kaikki julkiset metodit ovat "action"-metodeita — ne vastaavat URL-osoitteita.
public class AccountController : Controller
{
    // ── Riippuvuudet (injektoidaan konstruktorissa) ────────────────────────────
    // ASP.NET Core käyttää Dependency Injection (DI) -mallia:
    // palvelut ilmoitetaan Program.cs:ssä ja DI-säiliö luo ne automaattisesti.

    // UserManager: luo, muokkaa ja poistaa käyttäjiä, hallinnoi salasanoja ja rooleja.
    private readonly UserManager<ApplicationUser>   _userManager;

    // SignInManager: kirjaa käyttäjän sisään ja ulos, hallinnoi evästeitä.
    private readonly SignInManager<ApplicationUser> _signInManager;

    // ILogger: kirjoittaa diagnostiikkalokeja (ei audit-lokia vaan kehittäjän loki).
    private readonly ILogger<AccountController>     _logger;

    // ApplicationDbContext: EF Coren tietokantayhteys.
    private readonly ApplicationDbContext           _db;

    // IAuditService: kirjoittaa tietoturva-audit-lokimerkinnät.
    private readonly IAuditService                  _audit;

    // Konstruktori: ASP.NET Core kutsuu tätä automaattisesti joka pyynnön alussa.
    // Parametrit täytetään DI-säiliöstä — kehittäjä ei luo näitä olioita itse.
    public AccountController(
        UserManager<ApplicationUser>   userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController>     logger,
        ApplicationDbContext           db,
        IAuditService                  audit)
    {
        // Tallennetaan yksityisiin kenttiin myöhempää käyttöä varten.
        _userManager   = userManager;
        _signInManager = signInManager;
        _logger        = logger;
        _db            = db;
        _audit         = audit;
    }

    /// <summary>
    /// GET /Account/Login — Näyttää kirjautumislomakkeen. Jos käyttäjä
    /// on jo kirjautunut, ohjataan suoraan etusivulle.
    /// </summary>
    // [HttpGet] = tämä metodi vastaa vain GET-pyyntöihin.
    // GET = normaali sivulatauspyyntö (osoiterivin kirjoittaminen tai linkin klikkaus).
    [HttpGet]
    // [AllowAnonymous] = tämä sivu näytetään myös kirjautumattomille käyttäjille.
    // Ilman tätä [Authorize]-attribuutti ohjaisi takaisin kirjautumissivulle → ikuinen silmukka.
    [AllowAnonymous]
    // Parametri: returnUrl = sivun osoite, jolta tultiin (null jos suoraan kirjautumissivulle).
    public IActionResult Login(string? returnUrl = null)
    {
        // "User.Identity?.IsAuthenticated" = onko nykyinen käyttäjä jo kirjautunut.
        // "?." = null-safe operaattori: jos Identity on null, ei tule NullReferenceException.
        // "== true" tarkistaa eksplisiittisesti (vältetään null == true -tilanne).
        if (User.Identity?.IsAuthenticated == true)
            // Ohjataan jo kirjautunut käyttäjä etusivulle. Ei tarvita kirjautumislomaketta.
            return RedirectToAction("Index", "Home");

        // Luodaan tyhjä LoginViewModel ja täytetään ReturnUrl (säilyy lomakkeessa piilotettuna).
        // "return View(model)" renderöi Views/Account/Login.cshtml-tiedoston.
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    /// <summary>
    /// POST /Account/Login — Varsinainen kirjautumisen käsittely.
    /// "auth"-rate-limit suojaa brute-force-yrityksiltä.
    /// Epäonnistuneet yritykset lukitsevat tilin tilapäisesti (Identity).
    /// </summary>
    // [HttpPost] = tämä metodi vastaa vain POST-pyyntöihin (lomakkeen lähetys).
    [HttpPost]
    [AllowAnonymous]
    // [ValidateAntiForgeryToken] = tarkistaa CSRF-suojaustokenin.
    // CSRF (Cross-Site Request Forgery) = hyökkäys, jossa ulkopuolinen sivu
    // lähettää väärennettyjä pyyntöjä kirjautuneen käyttäjän nimissä.
    // ASP.NET lisää piilotettuna kentässä (@Html.AntiForgeryToken) tokenin,
    // jonka tämä attribuutti tarkistaa POST-pyynnön yhteydessä.
    [ValidateAntiForgeryToken]
    // [EnableRateLimiting("auth")] = rajoittaa kirjautumispyyntöjä.
    // "auth" viittaa Program.cs:ssä määriteltyyn rate-limit-käytäntöön.
    // Esim. max 5 yritystä 15 minuutissa — estää brute-force-hyökkäykset.
    [EnableRateLimiting("auth")]
    // "model" täytetään automaattisesti lomakkeen kentistä (model binding).
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        // ModelState.IsValid = tarkistaa kaikki DataAnnotations-validoinnit.
        // Jos esim. Email on tyhjä tai salasana puuttuu → IsValid = false.
        if (!ModelState.IsValid)
            // Palautetaan lomake uudelleen virheviestien kanssa.
            return View(model);

        // PasswordSignInAsync yrittää kirjata käyttäjän sisään.
        // Parametrit:
        //   model.Email      = käyttäjätunnus (sähköposti)
        //   model.Password   = salasana selkokielisenä (Identity vertaa tiivisteeseen)
        //   model.RememberMe = luodaanko pysyvä eväste
        //   lockoutOnFailure = true → tili lukitaan liian monien epäonnistumisten jälkeen
        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Kirjautuminen onnistui.
            // Haetaan käyttäjän tiedot lokitusta varten.
            var signedInUser = await _userManager.FindByEmailAsync(model.Email);
            // Kirjoitetaan diagnostiikkaloki (kehittäjälle, ei audit-lokia).
            _logger.LogInformation("Käyttäjä {UserId} kirjautui sisään.", signedInUser?.Id);
            // Kirjoitetaan audit-loki (tietoturva-jäljitys).
            await _audit.LogAsync("Login", details: model.Email);

            // Varmistetaan että ReturnUrl on paikallinen — estää avoimen ohjauksen
            // (open redirect) -hyökkäykset, joissa hyökkääjä voisi ohjata käyttäjän
            // ulkopuoliselle haitalliselle sivustolle kirjautumisen jälkeen.
            // Url.IsLocalUrl() = true vain jos osoite on saman verkkotunnuksen alla.
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                // Ohjataan takaisin sille sivulle, jolta tultiin.
                return Redirect(model.ReturnUrl);

            // Ei ReturnUrlia → ohjataan etusivulle.
            return RedirectToAction("Index", "Home");
        }

        // Kirjautuminen epäonnistui — kaksi eri syytä:

        if (result.IsLockedOut)
        {
            // Tili on lukittu liian monen väärän yrityksen takia.
            // Lokataan hash eikä sähköposti suoraan (tietosuoja lokissa).
            _logger.LogWarning("Käyttäjätili on lukittu (email-hash: {Hash}).", model.Email?.GetHashCode());
            // Audit-lokiin merkitään myös lukitustapahtuma.
            await _audit.LogAsync("LoginLockedOut", details: model.Email);
            // Näytetään käyttäjälle virheilmoitus (ei paljasteta kuinka kauan lukitus kestää).
            ModelState.AddModelError(string.Empty,
                "Tili on lukittu liian monen epäonnistuneen kirjautumisyrityksen vuoksi Yritä myöhemmin uudelleen.");
        }
        else
        {
            // Väärä salasana tai tuntematon käyttäjä.
            await _audit.LogAsync("LoginFailed", details: model.Email);
            // Virheilmoitus on tahallisen epämääräinen — ei paljasteta kumpi oli väärin
            // (turvallisuusperiaate: ei kerro onko kyseinen sähköposti rekisteröity).
            ModelState.AddModelError(string.Empty, "Virheellinen sähköpostiosoite tai salasana.");
        }

        // Palautetaan lomake uudelleen näytettäväksi virheviestien kanssa.
        return View(model);
    }

    /// <summary>POST /Account/Logout — Kirjautuu ulos ja merkitsee tapahtuman audit-lokiin.</summary>
    // [HttpPost] = uloskirjautuminen täytyy olla POST-pyyntö, ei GET.
    // Syy: GET-pyyntö voi laukaista kirjautumisen ulos vahingossa (esim. kuvalinkki sähköpostissa).
    [HttpPost]
    // [Authorize] = vain kirjautuneet käyttäjät voivat kirjautua ulos (loogista).
    [Authorize]
    [ValidateAntiForgeryToken]  // CSRF-suoja.
    public async Task<IActionResult> Logout()
    {
        // Kirjataan uloskirjautuminen audit-lokiin ennen kuin istunto tuhotaan.
        await _audit.LogAsync("Logout");
        // SignOutAsync poistaa autentikointievästeen → käyttäjä ei enää ole kirjautunut.
        await _signInManager.SignOutAsync();
        _logger.LogInformation("Käyttäjä kirjautui ulos.");
        // Ohjataan kirjautumissivulle (ei etusivulle — etusivu vaatii kirjautumisen).
        return RedirectToAction("Login", "Account");
    }

    /// <summary>GET /Account/Register — Adminin lomake uuden käyttäjän luomiseen.</summary>
    // [Authorize(Roles = "Admin")] = vain admin-roolissa olevat käyttäjät pääsevät tänne.
    [HttpGet]
    [Authorize(Roles = "Admin")]
    // Vapaaehtoinen customerId-parametri: jos tullaan asiakkaan sivulta, esitäytetään asiakas.
    public async Task<IActionResult> Register(int? customerId = null)
    {
        // Luodaan ViewModel ja täytetään pudotusvalikko.
        var model = new RegisterViewModel
        {
            CustomerId      = customerId,  // esitäytetty asiakas (null = ei valittu)
            CustomerOptions = await BuildCustomerOptions()  // async-kutsu tietokantaan
        };
        return View(model);
    }

    /// <summary>
    /// POST /Account/Register — Luo uuden käyttäjän adminin pyynnöstä.
    /// Vain admin saa kutsua. Asiakaskäyttäjälle valittu yritys on pakollinen.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        // Ensin validoidaan lomake.
        if (!ModelState.IsValid)
        {
            // Täytetään pudotusvalikko uudelleen (se ei säily lomakkeen lähetyksessä).
            model.CustomerOptions = await BuildCustomerOptions();
            return View(model);
        }

        // Roolin täytyy olla Admin tai Customer — muita ei hyväksytä.
        // Tämä estää sen että admin voisi luoda käyttäjän tuntemattomaan rooliin.
        if (model.Role != "Admin" && model.Role != "Customer")
            model.Role = "Customer";

        // Asiakaskäyttäjällä pitää olla yritys valittuna — tarkistetaan erikseen.
        if (model.Role == "Customer" && model.CustomerId is null)
        {
            // Lisätään manuaalinen validointivirhe CustomerId-kenttään.
            ModelState.AddModelError(nameof(model.CustomerId),
                "Asiakaskäyttäjälle on valittava yritys.");
            model.CustomerOptions = await BuildCustomerOptions();
            return View(model);
        }

        // Luodaan uusi ApplicationUser-olio (tietokantarivi).
        var user = new ApplicationUser
        {
            // UserName = sähköposti (ASP.NET Identityn käyttäjätunnus).
            UserName   = model.Email,
            Email      = model.Email,
            FullName   = model.FullName,
            // Admin-käyttäjällä ei ole yrityskytkentää (null).
            // Asiakaskäyttäjällä on yrityskytkentä.
            CustomerId = model.Role == "Customer" ? model.CustomerId : null
        };

        // CreateAsync luo käyttäjän tietokantaan JA tallentaa salasanan tiivisteenä (hash).
        // Selkokielinen salasana ei koskaan tallennu tietokantaan.
        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            // Käyttäjä luotu — lisätään rooliin.
            await _userManager.AddToRoleAsync(user, model.Role);
            _logger.LogInformation("Admin loi uuden käyttäjän {UserId} roolilla {Role}.", user.Id, model.Role);
            // Audit-loki: kuka, mitä, mille kohteelle, lisätieto.
            await _audit.LogAsync("UserCreated", "User", user.Id, $"{model.Email} / {model.Role}");
            // TempData = flashviesti — säilyy yhden uudelleenohjauksen yli (näytetään toast-viestinä).
            TempData["SuccessMessage"] = $"Käyttäjä {model.Email} luotu onnistuneesti ({model.Role}).";

            // Jos luotiin asiakaskäyttäjä, palataan asiakkaan tietoihin.
            if (model.Role == "Customer" && model.CustomerId.HasValue)
                return RedirectToAction("Details", "Customer", new { id = model.CustomerId.Value });

            // Muuten jäädään rekisteröintilomakkeelle (admin luo usein monta käyttäjää).
            return RedirectToAction(nameof(Register));
        }

        // Luominen epäonnistui — näytetään Identity:n virheilmoitukset.
        // "result.Errors" sisältää tiedon miksi luominen epäonnistui
        // (esim. salasana liian lyhyt, sähköposti jo käytössä).
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        model.CustomerOptions = await BuildCustomerOptions();
        return View(model);
    }

    /// <summary>GET /Account/ChangePassword — Salasanan vaihtolomake.</summary>
    [HttpGet]
    [Authorize]  // Kaikki kirjautuneet käyttäjät voivat vaihtaa salasanansa.
    public IActionResult ChangePassword()
    {
        // Palautetaan tyhjä lomake — ei tarvita tietokantakutsuja.
        return View(new ChangePasswordViewModel());
    }

    /// <summary>
    /// POST /Account/ChangePassword — Vaihtaa salasanan jos vanha salasana
    /// täsmää. RefreshSignInAsync uudistaa istuntoevästeen, jotta käyttäjä
    /// ei kirjaudu ulos vaihdon yhteydessä.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        // Validoidaan lomake ensin.
        if (!ModelState.IsValid)
            return View(model);

        // Haetaan kirjautuneen käyttäjän tiedot.
        // "User" = ClaimsPrincipal-olio (ASP.NET Coren istuntokonteksti).
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            // Jos istunto on vanhentunut, ohjataan kirjautumissivulle.
            return RedirectToAction(nameof(Login));

        // ChangePasswordAsync tarkistaa vanhan salasanan ja vaihtaa uuteen.
        // Vanha salasana validoidaan — ei voi vaihtaa ilman vanhaa salasanaa.
        var result = await _userManager.ChangePasswordAsync(
            user, model.CurrentPassword, model.NewPassword);

        if (result.Succeeded)
        {
            // Salasana vaihdettu. RefreshSignInAsync päivittää autentikointievästeen
            // uudelle salasanatiivisteelle — käyttäjä pysyy kirjautuneena.
            // Ilman tätä kutsua käyttäjä kirjautuisi automaattisesti ulos.
            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("Käyttäjä {UserId} vaihtoi salasanansa.", user.Id);
            // Audit-loki turvallisuustapahtumasta.
            await _audit.LogAsync("PasswordChanged", "User", user.Id);
            TempData["SuccessMessage"] = "Salasana vaihdettu onnistuneesti.";
            // Ohjataan takaisin salasanan vaihtosivulle (lomake tyhjenee uudelleenohjauksen myötä).
            return RedirectToAction(nameof(ChangePassword));
        }

        // Vaihto epäonnistui — näytetään virhe (esim. "Vanha salasana on väärä").
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    /// <summary>GET /Account/AccessDenied — Sivu johon ohjataan jos käyttöoikeus puuttuu.</summary>
    [HttpGet]
    [AllowAnonymous]  // Kaikki näkevät "pääsy estetty" -sivun.
    public IActionResult AccessDenied()
    {
        // Staattinen sivu — ei tarvita logiikkaa.
        return View();
    }

    /// <summary>
    /// POST /Account/SetLanguage — Tallentaa käyttäjän valitseman kielen
    /// evästeeseen, jonka ASP.NET Core lukee jokaisella pyynnöllä.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]  // Kielen voi vaihtaa myös kirjautumaton käyttäjä.
    [ValidateAntiForgeryToken]
    public IActionResult SetLanguage(string culture, string returnUrl = "/")
    {
        // Turvallisuus: sallitaan vain tunnetut kielet.
        // HashSet = tehokas joukko O(1) haulla — Contains() on nopea.
        var allowed = new HashSet<string> { "fi-FI", "sv-SE", "en-US" };
        // Jos lomakkeesta tulisi tuntematon kieli, käytetään oletuksena suomea.
        if (!allowed.Contains(culture)) culture = "fi-FI";

        // Tallennetaan kieli evästeeseen.
        // CookieRequestCultureProvider.DefaultCookieName = ".AspNetCore.Culture" (vakionimi).
        // CookieRequestCultureProvider.MakeCookieValue() muotoilee arvon oikeaan formaattiin.
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                // Eväste vanhenee vuoden kuluttua — ei tarvitse vaihtaa kieltä joka vierailulla.
                Expires  = DateTimeOffset.UtcNow.AddYears(1),
                // IsEssential = true = eväste on toiminnallisesti pakollinen (GDPR).
                IsEssential = true,
                // SameSite = Lax = suojaa CSRF-hyökkäyksiltä mutta sallii linkkien kautta navigoinnin.
                SameSite = SameSiteMode.Lax
            });

        // LocalRedirect on turvallisempi kuin Redirect:
        // se hyväksyy vain saman sivuston osoitteet (estää open redirect).
        return LocalRedirect(returnUrl);
    }

    // ── Apumetodit ────────────────────────────────────────────────────────────

    /// <summary>Rakentaa pudotusvalikon vaihtoehdot (aktiiviset asiakkaat aakkosjärjestyksessä).</summary>
    // Private = vain tämän kontrollerin sisällä käytettävä apumetodi.
    // async = käyttää asynkronista tietokantakutsua (ei blokkaa pyynnön käsittelyä).
    // Task<T> = asynkronisen metodin paluutyyppi.
    private async Task<IEnumerable<SelectListItem>> BuildCustomerOptions() =>
        // Haetaan tietokannasta vain aktiiviset asiakkaat.
        (await _db.Customers
            .Where(c => c.IsActive)           // Suodatetaan: vain aktiiviset.
            .OrderBy(c => c.CompanyName)      // Järjestetään: aakkosjärjestyksessä.
            .ToListAsync())                   // SQL → lista muistissa.
        // Muunnetaan SelectListItem-objekteiksi pudotusvalikkoa varten.
        // SelectListItem(text, value) = näkyvä teksti ja lomakkeesta lähetettävä arvo.
        .Select(c => new SelectListItem(c.CompanyName, c.Id.ToString()));
}
