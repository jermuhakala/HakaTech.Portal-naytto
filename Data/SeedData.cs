using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace HakaTech.Portal.Data;

public static class SeedData
{
    public static class Roles
    {
        public const string Admin    = "Admin";
        public const string Customer = "Customer";
    }

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext   = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // ── HUOM! DEMO-TILA ──────────────────────────────────────────────
        // Tyhjennetään ja luodaan tietokanta kokonaan uudestaan joka kerta
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
        // ─────────────────────────────────────────────────────────────────

        // 1. Luo roolit
        foreach (var role in new[] { Roles.Admin, Roles.Customer })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // 2. Luo Yritysasiakkaat
        var digimolli = new Customer { CompanyName = "DigiMölli Oy", BusinessId = "1234567-8", ContactEmail = "info@digimolli.fi", Phone = "0401234567", Address = "Ohjelmistotie 1, Helsinki" };
        var kivikangas = new Customer { CompanyName = "Kivikangas Rakennus", BusinessId = "9876543-2", ContactEmail = "urakointi@kivikangas.fi", Phone = "0509876543", Address = "Betonikuja 5, Tampere" };
        var techsol = new Customer { CompanyName = "TechSolutions Finland", BusinessId = "1122334-5", ContactEmail = "hello@techsolutions.fi", Phone = "0451122334", Address = "Kuitukaapeli 8, Espoo" };

        dbContext.Customers.AddRange(digimolli, kivikangas, techsol);
        await dbContext.SaveChangesAsync();

        // 3. Luo Admin-käyttäjät
        const string defaultPassword = "HakaTech2025!";
        var admin1 = await CreateUserAsync(userManager, "admin@hakatech.fi", "Järjestelmänvalvoja", null, Roles.Admin, defaultPassword);
        var admin2 = await CreateUserAsync(userManager, "support@hakatech.fi", "Asiakastuki", null, Roles.Admin, defaultPassword);

        // 4. Luo Asiakaskäyttäjät yrityksille
        var matti = await CreateUserAsync(userManager, "matti@digimolli.fi", "Matti Meikäläinen", digimolli.Id, Roles.Customer, defaultPassword);
        var kalle = await CreateUserAsync(userManager, "kalle@kivikangas.fi", "Kalle Kivikangas", kivikangas.Id, Roles.Customer, defaultPassword);
        var miia = await CreateUserAsync(userManager, "miia@techsolutions.fi", "Miia Mäkelä", techsol.Id, Roles.Customer, defaultPassword);

        // 5. Luo Palvelusopimukset
        dbContext.Contracts.AddRange(
            new Contract { CustomerId = digimolli.Id, Type = ContractType.SupportBusiness, StartDate = DateTime.UtcNow.AddMonths(-6), EndDate = DateTime.UtcNow.AddMonths(6), MonthlyPrice = 450.00m, Description = "Arkipäivien tukipalvelu" },
            new Contract { CustomerId = kivikangas.Id, Type = ContractType.OneTime, StartDate = DateTime.UtcNow.AddMonths(-1), EndDate = DateTime.UtcNow.AddMonths(1), MonthlyPrice = 0, Description = "Kertaluontoinen laiteasennus" },
            new Contract { CustomerId = techsol.Id, Type = ContractType.Support24_7, StartDate = DateTime.UtcNow.AddYears(-1), EndDate = DateTime.UtcNow.AddYears(1), MonthlyPrice = 1200.00m, Description = "Kriittinen 24/7 SLA ylläpito" }
        );
        await dbContext.SaveChangesAsync();

        // 6. Luo Tiketit & Kommentit
        var t1 = new Ticket { Title = "Sisäänkirjautuminen epäonnistuu järjestelmään", Description = "Käyttäjät eivät pääse kirjautumaan toiminnanohjausjärjestelmään.", Category = TicketCategory.Software, Priority = TicketPriority.High, Status = TicketStatus.Resolved, CreatedAt = DateTime.UtcNow.AddDays(-5), ResolvedAt = DateTime.UtcNow.AddDays(-1), CustomerId = digimolli.Id, CreatedByUserId = matti.Id, AssignedToUserId = admin1.Id };
        var t2 = new Ticket { Title = "Uusien sähköpostitilien luominen", Description = "Tarvitsemme 5 uutta tiliä kesätyöntekijöille.", Category = TicketCategory.Email, Priority = TicketPriority.Low, Status = TicketStatus.Open, CreatedAt = DateTime.UtcNow.AddDays(-1), CustomerId = digimolli.Id, CreatedByUserId = matti.Id };
        var t3 = new Ticket { Title = "Tulostin ei toimi konttorilla", Description = "Kakkoskerroksen tulostin jumittaa heti käynnistyksen jälkeen.", Category = TicketCategory.Hardware, Priority = TicketPriority.Normal, Status = TicketStatus.Closed, CreatedAt = DateTime.UtcNow.AddDays(-20), ResolvedAt = DateTime.UtcNow.AddDays(-19), CustomerId = kivikangas.Id, CreatedByUserId = kalle.Id, AssignedToUserId = admin2.Id };
        var t4 = new Ticket { Title = "Palvelin ei vastaa pingeihin", Description = "Nettisivumme ja API-rajapinnat ovat alhaalla!", Category = TicketCategory.Network, Priority = TicketPriority.Critical, Status = TicketStatus.InProgress, CreatedAt = DateTime.UtcNow.AddHours(-2), CustomerId = techsol.Id, CreatedByUserId = miia.Id, AssignedToUserId = admin2.Id };
        var t5 = new Ticket { Title = "VPN yhteyden pätkiminen", Description = "Kotikonttorilta työskentelevät valittavat satunnaisista VPN katkoksista.", Category = TicketCategory.Network, Priority = TicketPriority.Normal, Status = TicketStatus.WaitingCustomer, CreatedAt = DateTime.UtcNow.AddDays(-3), CustomerId = techsol.Id, CreatedByUserId = miia.Id, AssignedToUserId = admin1.Id };

        dbContext.Tickets.AddRange(t1, t2, t3, t4, t5);
        await dbContext.SaveChangesAsync();

        dbContext.TicketComments.AddRange(
            new TicketComment { TicketId = t1.Id, AuthorId = matti.Id, Content = "Ongelma alkoi tänä aamuna klo 08:00.", CreatedAt = DateTime.UtcNow.AddDays(-5).AddHours(1) },
            new TicketComment { TicketId = t1.Id, AuthorId = admin1.Id, Content = "Tarkistan AD-palvelimen lokit.", CreatedAt = DateTime.UtcNow.AddDays(-5).AddHours(2) },
            new TicketComment { TicketId = t1.Id, AuthorId = admin1.Id, Content = "Vika löytyi synkronointivirheestä. Korjattu, testaatteko uudelleen?", CreatedAt = DateTime.UtcNow.AddDays(-1).AddHours(-4) },
            new TicketComment { TicketId = t1.Id, AuthorId = matti.Id, Content = "Nyt toimii, kiitos!", CreatedAt = DateTime.UtcNow.AddDays(-1).AddHours(-2) },
            
            new TicketComment { TicketId = t5.Id, AuthorId = admin1.Id, Content = "Olemme päivittäneet palomuurin firmiksen. Pätkiikö yhteys vielä?", CreatedAt = DateTime.UtcNow.AddDays(-1) }
        );
        await dbContext.SaveChangesAsync();

        // 7. Luo Laskuja ja Laskurivejä
        var inv1 = new Invoice { CustomerId = digimolli.Id, InvoiceNumber = "INV-2026-001", Status = InvoiceStatus.Paid, InvoiceDate = DateTime.UtcNow.AddMonths(-1).AddDays(2), DueDate = DateTime.UtcNow.AddMonths(-1).AddDays(16), PaidAt = DateTime.UtcNow.AddMonths(-1).AddDays(15), Notes = "Säännöllinen ylläpito" };
        var inv2 = new Invoice { CustomerId = digimolli.Id, InvoiceNumber = "INV-2026-002", Status = InvoiceStatus.Overdue, InvoiceDate = DateTime.UtcNow.AddMonths(-2), DueDate = DateTime.UtcNow.AddMonths(-2).AddDays(14), Notes = "Lisälaitteiden asennus ja konfigurointi" };
        var inv3 = new Invoice { CustomerId = kivikangas.Id, InvoiceNumber = "INV-2026-003", Status = InvoiceStatus.Draft, InvoiceDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(14) };
        var inv4 = new Invoice { CustomerId = techsol.Id, InvoiceNumber = "INV-2026-004", Status = InvoiceStatus.Sent, InvoiceDate = DateTime.UtcNow.AddDays(-2), DueDate = DateTime.UtcNow.AddDays(12), Notes = "Palvelinsalipaikan kuukausiveloitus" };

        dbContext.Invoices.AddRange(inv1, inv2, inv3, inv4);
        await dbContext.SaveChangesAsync();

        dbContext.InvoiceLines.AddRange(
            new InvoiceLine { InvoiceId = inv1.Id, Description = "IT Tuki Business kk-maksu", Quantity = 1, UnitPrice = 450.00m },
            new InvoiceLine { InvoiceId = inv1.Id, Description = "Lisätyö: Työpisteiden asennus", Quantity = 5, UnitPrice = 85.00m },

            new InvoiceLine { InvoiceId = inv2.Id, Description = "Kytkimen hankinta", Quantity = 1, UnitPrice = 320.00m },
            new InvoiceLine { InvoiceId = inv2.Id, Description = "Asennus ja konfigurointi", Quantity = 3.5m, UnitPrice = 90.00m },

            new InvoiceLine { InvoiceId = inv3.Id, Description = "Tulostimen korjauskäynti", Quantity = 1, UnitPrice = 150.00m },

            new InvoiceLine { InvoiceId = inv4.Id, Description = "SLA 24/7 ylläpito", Quantity = 1, UnitPrice = 1200.00m },
            new InvoiceLine { InvoiceId = inv4.Id, Description = "Palvelintilamaksu", Quantity = 1, UnitPrice = 500.00m }
        );
        await dbContext.SaveChangesAsync();

        // 8. Palvelukatalogi
        var svc1 = new ServiceCatalogItem { Name = "Palvelinhuolto", Category = "Ylläpito", Description = "Palvelinympäristön huolto ja päivitykset. Sisältää käyttöjärjestelmäpäivitykset, varmuuskopioinnin tarkistuksen ja suorituskyvyn optimoinnin.", Price = 290.00m };
        var svc2 = new ServiceCatalogItem { Name = "Tietoturva-auditointi", Category = "Tietoturva", Description = "Kattava tietoturva-auditointi organisaatiollesi. Selvitämme haavoittuvuudet ja annamme toimenpidesuositukset.", Price = 1200.00m };
        var svc3 = new ServiceCatalogItem { Name = "Verkon suunnittelu ja toteutus", Category = "Verkko", Description = "Uuden verkkoinfrastruktuurin suunnittelu ja käyttöönotto. Langallinen ja langaton verkko, palomuurit ja VPN-ratkaisut." };
        var svc4 = new ServiceCatalogItem { Name = "Microsoft 365 -käyttöönotto", Category = "Pilvipalvelut", Description = "M365-ympäristön suunnittelu, lisensointi ja käyttöönotto käyttäjäkoulutuksineen.", Price = 850.00m };
        var svc5 = new ServiceCatalogItem { Name = "Varmuuskopiointiratkaisu", Category = "Ylläpito", Description = "Automaattinen varmuuskopiointiratkaisu kriittiselle datalle. Pilvi- tai paikallinen tallennus, testattava palautus." };
        var svc6 = new ServiceCatalogItem { Name = "IT-strategia konsultointi", Category = "Konsultointi", Description = "Autetaan yritystä IT-strategian luomisessa ja digitalisaatiossa. Toteutamme teknologiakartoituksen ja tiekartan.", Price = 200.00m };

        dbContext.ServiceCatalogItems.AddRange(svc1, svc2, svc3, svc4, svc5, svc6);
        await dbContext.SaveChangesAsync();

        // Demo-tarjouspyynnöt
        dbContext.QuoteRequests.AddRange(
            new QuoteRequest { ServiceCatalogItemId = svc1.Id, CustomerId = digimolli.Id, CreatedByUserId = matti.Id, Message = "Tarvitsemme vuosittaisen palvelinhuollon sopimuksemme piiriin.", Status = QuoteRequestStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new QuoteRequest { ServiceCatalogItemId = svc2.Id, CustomerId = techsol.Id, CreatedByUserId = miia.Id, Message = "Haluaisimme auditoinnin ennen vuoden loppua, onko mahdollista?", Status = QuoteRequestStatus.InProgress, AdminNotes = "Sovitaan ajankohta viikolla 20.", CreatedAt = DateTime.UtcNow.AddDays(-7) }
        );
        await dbContext.SaveChangesAsync();

        // 9. Tiedotteet
        dbContext.Announcements.AddRange(
            new Announcement
            {
                Title           = "Huoltokatko pe 23.5. klo 22:00–01:00",
                Content         = "Suoritamme palvelinpäivityksiä. Portaali ja sähköpostipalvelut voivat olla tilapäisesti poissa käytöstä.",
                Type            = AnnouncementType.Maintenance,
                ValidFrom       = new DateTime(2026, 5, 23, 19, 0, 0, DateTimeKind.Utc),
                ValidUntil      = new DateTime(2026, 5, 24,  1, 0, 0, DateTimeKind.Utc),
                IsPublished     = true,
                CreatedByUserId = admin1.Id,
                CreatedAt       = DateTime.UtcNow.AddDays(-1)
            },
            new Announcement
            {
                Title           = "Uusi palvelukatalogi käytössä",
                Content         = "Voit nyt pyytää tarjouksia palveluistamme suoraan portaalista.",
                Type            = AnnouncementType.Info,
                IsPublished     = true,
                CreatedByUserId = admin1.Id,
                CreatedAt       = DateTime.UtcNow
            }
        );
        await dbContext.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string fullName, int? customerId, string role, string password)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            CustomerId = customerId,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, role);
        return user;
    }
}
