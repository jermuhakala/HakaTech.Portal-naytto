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

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// ── Tietokanta ──────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Identity ────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Salasanavaatimukset
    options.Password.RequiredLength         = 10;
    options.Password.RequireUppercase       = true;
    options.Password.RequireLowercase       = true;
    options.Password.RequireDigit           = true;
    options.Password.RequireNonAlphanumeric = true;

    // Käyttäjätilin lukitusasetukset
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);

    // Vaaditaanko sähköpostin vahvistus — päällä tuotannossa
    options.SignIn.RequireConfirmedEmail = !builder.Environment.IsDevelopment();

    // Käyttäjänimi vain sallituilla merkeillä
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── Kirjautumis- & eväste-asetukset ─────────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath          = "/Account/Login";
    options.LogoutPath         = "/Account/Logout";
    options.AccessDeniedPath   = "/Account/AccessDenied";
    options.ExpireTimeSpan     = TimeSpan.FromHours(8);
    options.SlidingExpiration  = true;

    options.Cookie.Name         = "HakaTechPortal.Auth";
    options.Cookie.HttpOnly     = true;
    options.Cookie.SameSite     = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
    options.HttpOnly              = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.Secure                = CookieSecurePolicy.Always;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name         = "HakaTechPortal.Antiforgery";
    options.Cookie.HttpOnly     = true;
    options.Cookie.SameSite     = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddTransient<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddSingleton<IHtmlSanitizerService, HtmlSanitizerService>();
builder.Services.AddHttpContextAccessor();

// ── Guacamole ────────────────────────────────────────────────────
builder.Services.Configure<GuacamoleSettings>(
    builder.Configuration.GetSection("GuacamoleSettings"));

// ── Data Protection: persistoidut avaimet ───────────────────────
var dpBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("HakaTechPortal");

var dpKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(dpKeyPath))
{
    Directory.CreateDirectory(dpKeyPath);
    dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(dpKeyPath));
}

builder.Services.AddHttpClient<IGuacamoleService, GuacamoleService>();

builder.Services.AddSignalR();
builder.Services.AddLocalization();
builder.Services.AddControllersWithViews()
    .AddViewLocalization();

// ── Rate limiting ───────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Tiukka ikkuna kirjautumis- ja rekisteröintipäätepisteille (per-IP)
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

    // Yleinen suojamekanismi
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

// ── HSTS-asetukset ──────────────────────────────────────────────
builder.Services.AddHsts(options =>
{
    options.Preload           = true;
    options.IncludeSubDomains = true;
    options.MaxAge            = TimeSpan.FromDays(365);
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

// ── Seed: luo roolit & admin-käyttäjä jos ei ole ────────────────
await SeedData.InitializeAsync(app.Services);

app.UseForwardedHeaders();

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
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Frame-Options"]        = "DENY";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"]     = "geolocation=(), microphone=(), camera=()";

    // CSP – salli vain omat skriptit ja valikoidut CDN:t.
    // Huom: mikäli Razor-näkymissä on inline-skriptejä,
    // 'unsafe-inline' on toistaiseksi tarpeen.
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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCookiePolicy();

var supportedCultures = new[] { "fi-FI", "sv-SE", "en-US" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("fi-FI")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
localizationOptions.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication(); // ENSIN Authentication
app.UseAuthorization();  // SITTEN Authorization

app.MapHub<TicketHub>("/hubs/ticket");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
