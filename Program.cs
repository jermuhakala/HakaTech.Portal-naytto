using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Tietokanta ──────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Identity ────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Salasanavaatimukset
    options.Password.RequiredLength         = 8;
    options.Password.RequireUppercase       = true;
    options.Password.RequireDigit           = true;
    options.Password.RequireNonAlphanumeric = false;

    // Käyttäjätilin lukitusasetukset
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);

    // Vaaditaanko sähköpostin vahvistus
    options.SignIn.RequireConfirmedEmail = false; // true tuotannossa
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── Kirjautumis- & uloskirjautumispolut ─────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath          = "/Account/Login";
    options.LogoutPath         = "/Account/Logout";
    options.AccessDeniedPath   = "/Account/AccessDenied";
    options.ExpireTimeSpan     = TimeSpan.FromHours(8);
    options.SlidingExpiration  = true;
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

// ── Seed: luo roolit & admin-käyttäjä jos ei ole ────────────────
await SeedData.InitializeAsync(app.Services);

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication(); // ENSIN Authentication
app.UseAuthorization();  // SITTEN Authorization

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
