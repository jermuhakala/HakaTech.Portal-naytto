// =============================================================================
// Program.cs — Sovelluksen käynnistystiedosto
// -----------------------------------------------------------------------------
// Tämä tiedosto rakentaa ja käynnistää koko ASP.NET Core -sovelluksen.
// Täällä kerrotaan mm. mitä palveluita käytetään (tietokanta, kirjautuminen,
// sähköposti, tiedostojen tallennus jne.) ja miten HTTP-pyynnöt käsitellään
// (tietoturvaotsikot, kielen valinta, reititys).
// =============================================================================

using HakaTech.Portal.Data;
using HakaTech.Portal.Hubs;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using System.Threading.RateLimiting;

// QuestPDF (PDF-kirjasto) tarvitsee lisenssityypin määrittelyn ennen käyttöä.
// Community-lisenssi sallii ilmaisen käytön pienissä yrityksissä ja avoimissa projekteissa.
QuestPDF.Settings.License = LicenseType.Community;

// Luo "rakentaja", jolla sovellus ja sen palvelut konfiguroidaan.
var builder = WebApplication.CreateBuilder(args);

// ── Tietokanta ──────────────────────────────────────────────────
// Rekisteröidään Entity Framework Core -tietokantakonteksti,
// joka käyttää SQL Serveriä. Yhteysmerkkijono luetaan appsettings.json:ista.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Identity (käyttäjien hallinta ja kirjautuminen) ─────────────
// ASP.NET Core Identity hoitaa käyttäjien tallennuksen, salasanat,
// roolit ja kirjautumisen. Alla määritellään turvallisuusvaatimukset.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Salasanavaatimukset — vähintään 10 merkkiä, oltava iso ja pieni kirjain,
    // numero sekä erikoismerkki. Estää helposti arvattavat salasanat.
    options.Password.RequiredLength         = 10;
    options.Password.RequireUppercase       = true;
    options.Password.RequireLowercase       = true;
    options.Password.RequireDigit           = true;
    options.Password.RequireNonAlphanumeric = true;

    // Käyttäjätilin lukitusasetukset: 5 epäonnistuneen yrityksen jälkeen
    // tili lukkiutuu 15 minuutiksi. Estää brute-force -hyökkäykset.
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);

    // Sähköpostin vahvistus vaaditaan vain tuotannossa, jotta
    // kehitysympäristössä on helpompi testata.
    options.SignIn.RequireConfirmedEmail = !builder.Environment.IsDevelopment();

    // Sähköpostiosoite saa esiintyä tietokannassa vain kerran.
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>() // tallennus EF Coren kautta tietokantaan
.AddDefaultTokenProviders();                      // tokenit (esim. salasanan nollaus)

// ── Kirjautumis- & eväste-asetukset ─────────────────────────────
// Määrittelee miten kirjautumisevästeet (cookies) toimivat ja
// mihin osoitteisiin käyttäjä ohjataan eri tilanteissa.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath          = "/Account/Login";          // mihin ohjataan kun ei kirjautunut
    options.LogoutPath         = "/Account/Logout";         // uloskirjautumisen polku
    options.AccessDeniedPath   = "/Account/AccessDenied";   // mihin ohjataan jos puuttuu oikeudet
    options.ExpireTimeSpan     = TimeSpan.FromHours(8);     // istunto vanhenee 8 tunnissa
    options.SlidingExpiration  = true;                      // vanhentumisaika pitenee aktiivikäytössä

    // Eväste merkitään turvallisilla lipuilla:
    //  - HttpOnly: JavaScript ei pääse käsiksi (XSS-suoja)
    //  - SameSite=Strict: ei lähetetä toiselta sivustolta tulevien pyyntöjen mukana (CSRF-suoja)
    //  - Secure=Always: lähetetään vain HTTPS:n yli
    options.Cookie.Name         = "HakaTechPortal.Auth";
    options.Cookie.HttpOnly     = true;
    options.Cookie.SameSite     = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Yleinen evästepolitiikka: kaikki evästeet käyttävät tiukimpia asetuksia.
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
    options.HttpOnly              = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.Secure                = CookieSecurePolicy.Always;
});

// Antiforgery (CSRF-suoja): luodaan oma token-eväste lomakkeiden suojaamiseen.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name         = "HakaTechPortal.Antiforgery";
    options.Cookie.HttpOnly     = true;
    options.Cookie.SameSite     = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Rekisteröidään omat palvelut (DI-kontti). Näillä määritellään mistä
// luokista konkreettiset toteutukset haetaan kun rajapintaa pyydetään.
builder.Services.AddTransient<IEmailService, SmtpEmailService>();              // sähköpostien lähetys
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();    // tiedostojen tallennus levylle
builder.Services.AddScoped<IAuditService, AuditService>();                     // audit-lokitus
builder.Services.AddSingleton<IHtmlSanitizerService, HtmlSanitizerService>();  // HTML:n puhdistus (XSS-suoja)
builder.Services.AddHttpContextAccessor();                                     // pääsy HttpContextiin palveluista

// ── Guacamole (etätyöpöytäyhteys) ────────────────────────────────
// Ladataan Guacamole-palvelimen asetukset appsettings.json -tiedostosta.
builder.Services.Configure<GuacamoleSettings>(
    builder.Configuration.GetSection("GuacamoleSettings"));

// ── Data Protection: salausavainten persistointi ────────────────
// ASP.NET Core käyttää salausavaimia mm. evästeiden ja tokenien
// salaamiseen. Avaimet tallennetaan levylle, jotta ne säilyvät
// uudelleenkäynnistysten yli ja monen palvelimen kesken.
var dpBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("HakaTechPortal");

var dpKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(dpKeyPath))
{
    Directory.CreateDirectory(dpKeyPath);
    dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(dpKeyPath));
}

// HTTP-asiakas Guacamole REST-API:n kutsuihin.
builder.Services.AddHttpClient<IGuacamoleService, GuacamoleService>();

// SignalR mahdollistaa reaaliaikaisen viestinnän selaimen ja palvelimen
// välillä (esim. tikettien live-päivitykset).
builder.Services.AddSignalR();

// Lokalisointi (monikielisyys): käännösten lataus resurssitiedostoista.
builder.Services.AddLocalization();

// MVC-controllerit ja Razor-näkymät, näkymien lokalisointi mukana.
builder.Services.AddControllersWithViews()
    .AddViewLocalization();

// ── Rate limiting (pyyntörajoitus) ──────────────────────────────
// Estää saman IP:n liiallisen kutsumäärän, jolloin sovellus on
// suojassa palvelunestohyökkäyksiltä ja brute-force -yrityksiltä.
builder.Services.AddRateLimiter(options =>
{
    // Liika pyyntöjä → vastauksena 429 Too Many Requests.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // "auth" -politiikka: tiukempi raja kirjautumis- ja rekisteröintisivuille.
    // Sallii max 10 pyyntöä per IP per minuutti.
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit       = 10,
                Window            = TimeSpan.FromMinutes(1),
                QueueLimit        = 0,
                AutoReplenishment = true
            }));

    // Yleinen raja kaikille muille pyynnöille: 200 / IP / minuutti.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit       = 200,
                Window            = TimeSpan.FromMinutes(1),
                QueueLimit        = 0,
                AutoReplenishment = true
            }));
});

// ── HSTS (HTTP Strict Transport Security) ───────────────────────
// Kertoo selaimelle että sivustoa saa käyttää vain HTTPS:n yli.
// Aktivoituu vain tuotannossa (ks. app.UseHsts() alempana).
builder.Services.AddHsts(options =>
{
    options.Preload           = true;
    options.IncludeSubDomains = true;
    options.MaxAge            = TimeSpan.FromDays(365);
});

// Käänteisproxyn (esim. nginx) lähettämät otsikot otetaan huomioon,
// jotta esim. asiakkaan IP-osoite ja HTTPS-tieto välittyvät oikein.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// Rakennetaan WebApplication kaikilla yllä rekisteröidyillä palveluilla.
var app = builder.Build();

// ── Alustusdata: roolit ja admin-käyttäjä ───────────────────────
// Luodaan tarvittavat roolit ja oletuskäyttäjä, jos niitä ei vielä
// ole tietokannassa. Tämä tapahtuu jokaisen käynnistyksen yhteydessä.
await SeedData.InitializeAsync(app.Services);

// HTTP-putken konfigurointi alkaa tästä — middlewaret ajetaan järjestyksessä.

// Ottaa proxyn lähettämät X-Forwarded-* -otsikot käyttöön.
app.UseForwardedHeaders();

// Kehityksessä näytetään tarkka virhesivu, tuotannossa siisti virhesivu + HSTS.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ── Tietoturvaotsikot ───────────────────────────────────────────
// Lisätään jokaiseen vastaukseen joukko otsikoita, jotka suojaavat
// selainta yleisiltä hyökkäyksiltä (clickjacking, MIME-sniffing jne.).
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Frame-Options"]        = "DENY";                                // estää iframe-upotuksen
    headers["X-Content-Type-Options"] = "nosniff";                             // estää content-type -arvauksen
    headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";     // rajoittaa Referer-otsikkoa
    headers["Permissions-Policy"]     = "geolocation=(), microphone=(), camera=()"; // estää selainominaisuuksia

    // Content-Security-Policy (CSP) rajoittaa mistä lähteistä selain saa
    // ladata skriptejä, tyylejä, fontteja ja kuvia. Tämä on tehokas
    // suoja XSS-hyökkäyksiä vastaan.
    // Huom: 'unsafe-inline' on toistaiseksi tarpeen Razor-näkymien
    // sisäänkirjoitettujen skriptien vuoksi.
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.googleapis.com; " +
        "font-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.gstatic.com data:; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' wss: ws:; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    await next();
});

app.UseHttpsRedirection(); // ohjaa HTTP → HTTPS
app.UseStaticFiles();      // tarjoaa tiedostot wwwroot/-kansiosta
app.UseCookiePolicy();     // pakottaa evästepolitiikan päälle

// ── Kielen valinta ──────────────────────────────────────────────
// Sovellus tukee suomea, ruotsia ja englantia. Käyttäjän valinta
// muistetaan evästeessä — siksi CookieRequestCultureProvider lisätään ensimmäiseksi.
var supportedCultures = new[] { "fi-FI", "sv-SE", "en-US" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("fi-FI")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
localizationOptions.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
app.UseRequestLocalization(localizationOptions);

app.UseRouting();      // tunnistaa minkä controllerin/actionin pyyntö osuu
app.UseRateLimiter();  // pyyntörajoitus aktiiviseksi

// JÄRJESTYS ON TÄRKEÄ:
// 1) Authentication selvittää KUKA käyttäjä on (kirjautunutko)
// 2) Authorization tarkistaa MITÄ tämä saa tehdä
app.UseAuthentication();
app.UseAuthorization();

// SignalR-hub tikettien reaaliaikaiseen viestintään.
app.MapHub<TicketHub>("/hubs/ticket");

// Oletusreititys MVC:lle: /Controller/Action/id
// Esim. /Ticket/Details/5 → TicketController.Details(5)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Käynnistää sovelluksen ja jää odottamaan HTTP-pyyntöjä.
app.Run();
