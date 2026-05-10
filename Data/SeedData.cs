using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Data;

/// <summary>
/// Sovelluksen alustusdata. Tämä luokka luo tyhjään tietokantaan
/// roolit, esimerkkikäyttäjät, asiakkaat, tikettejä, laskuja jne.
/// jotta sovellus on heti valmis demottavaksi ja kehitettäväksi.
///
/// InitializeAsync ajetaan jokaisen sovelluksen käynnistyksen yhteydessä,
/// mutta data luodaan vain kerran (jos asiakkaita ei ole vielä tietokannassa).
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Sovelluksen kaksi käyttäjäroolia. Admin = ylläpitäjä, Customer = loppuasiakas.
    /// Vakiot, jotta nimet eivät pääse kirjoitusvirheinä koodiin.
    /// </summary>
    public static class Roles
    {
        public const string Admin    = "Admin";
        public const string Customer = "Customer";
    }

    /// <summary>
    /// Pääfunktio: ajaa migraatiot ja täyttää kannan demo-datalla
    /// jos kanta on tyhjä.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        // Luodaan oma palvelu-scope, jotta DbContext ja muut scoped-palvelut
        // saadaan käyttöön staattisessa metodissa.
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext   = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ajetaan kaikki vielä ajamattomat migraatiot. Tämä on idempotentti —
        // jos migraatiot on jo ajettu, mitään ei tapahdu.
        await dbContext.Database.MigrateAsync();

        // Jos asiakkaita on jo (= seed on tehty aiemmin), ei tehdä mitään.
        if (await dbContext.Customers.AnyAsync())
            return;

        // 1. Luo roolit (Admin & Customer) jos eivät ole olemassa
        foreach (var role in new[] { Roles.Admin, Roles.Customer })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // 2. Luo demo-yritysasiakkaat (kolme erilaista yritystä)
        var digimolli = new Customer { CompanyName = "DigiMölli Oy", BusinessId = "1234567-8", ContactEmail = "info@digimolli.fi", Phone = "0401234567", Address = "Ohjelmistotie 1, Helsinki" };
        var kivikangas = new Customer { CompanyName = "Kivikangas Rakennus", BusinessId = "9876543-2", ContactEmail = "urakointi@kivikangas.fi", Phone = "0509876543", Address = "Betonikuja 5, Tampere" };
        var techsol = new Customer { CompanyName = "TechSolutions Finland", BusinessId = "1122334-5", ContactEmail = "hello@techsolutions.fi", Phone = "0451122334", Address = "Kuitukaapeli 8, Espoo" };

        dbContext.Customers.AddRange(digimolli, kivikangas, techsol);
        await dbContext.SaveChangesAsync();

        // 3. Luo Admin-käyttäjät (HakaTechin oma henkilöstö, ei kuulu mihinkään asiakkaaseen)
        // Demo-salasana on tarkoituksella sama kaikille — vaihdettava tuotannossa.
        const string defaultPassword = "HakaTech2025!";
        var admin1 = await CreateUserAsync(userManager, "admin@hakatech.fi", "Järjestelmänvalvoja", null, Roles.Admin, defaultPassword);
        var admin2 = await CreateUserAsync(userManager, "support@hakatech.fi", "Asiakastuki", null, Roles.Admin, defaultPassword);

        // 4. Luo asiakaskäyttäjät yrityksille (tavallisia portaalin loppukäyttäjiä).
        // Yhdellä käyttäjällä (Matti) on lisäksi customer-admin-oikeus omassa yrityksessään.
        var matti = await CreateUserAsync(userManager, "matti@digimolli.fi", "Matti Meikäläinen", digimolli.Id, Roles.Customer, defaultPassword, isCustomerAdmin: true);
        var kalle = await CreateUserAsync(userManager, "kalle@kivikangas.fi", "Kalle Kivikangas", kivikangas.Id, Roles.Customer, defaultPassword);
        var miia = await CreateUserAsync(userManager, "miia@techsolutions.fi", "Miia Mäkelä", techsol.Id, Roles.Customer, defaultPassword);
        // Lisäkäyttäjä DigiMöllille (pääkäyttäjän hallitsema)
        await CreateUserAsync(userManager, "laura@digimolli.fi", "Laura Leinonen", digimolli.Id, Roles.Customer, defaultPassword);

        // 5. Luo palvelusopimukset (Contract) — määrittelevät mitä tasoista palvelua kukin asiakas saa
        dbContext.Contracts.AddRange(
            new Contract { CustomerId = digimolli.Id, Type = ContractType.SupportBusiness, StartDate = DateTime.UtcNow.AddMonths(-6), EndDate = DateTime.UtcNow.AddMonths(6), MonthlyPrice = 450.00m, Description = "Arkipäivien tukipalvelu" },
            new Contract { CustomerId = kivikangas.Id, Type = ContractType.OneTime, StartDate = DateTime.UtcNow.AddMonths(-1), EndDate = DateTime.UtcNow.AddMonths(1), MonthlyPrice = 0, Description = "Kertaluontoinen laiteasennus" },
            new Contract { CustomerId = techsol.Id, Type = ContractType.Support24_7, StartDate = DateTime.UtcNow.AddYears(-1), EndDate = DateTime.UtcNow.AddYears(1), MonthlyPrice = 1200.00m, Description = "Kriittinen 24/7 SLA ylläpito" }
        );
        await dbContext.SaveChangesAsync();

        // 6. Luo demo-tikettejä eri tilassa (Open, Resolved, Closed, InProgress, WaitingCustomer)
        //    sekä niihin liittyviä kommentteja, jotta tikettilistasta saa heti realistisen kuvan
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

        // 7. Luo demo-laskuja eri tiloissa (Paid, Overdue, Draft, Sent) ja niiden laskurivit
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

        // 8. Palvelukatalogi: HakaTechin myytävät palvelut, joista asiakkaat voivat pyytää tarjouksen
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

        // 9. Tiedotteet (Announcements): admin näyttää näitä etusivulla — esim. huoltokatkot
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

        // 10. Etätyöpöytäyhteydet — yksi demo-RDP yhteys Guacamolen kautta.
        //     Salasana salataan ennen tallennusta Data Protection API:lla.
        var guacamole = scope.ServiceProvider.GetRequiredService<IGuacamoleService>();

        dbContext.RemoteDesktopConnections.AddRange(
            new RemoteDesktopConnection
            {
                Name                 = "HakaTech targethost (RDP)",
                Protocol             = RemoteDesktopProtocol.Rdp,
                Hostname             = "20.223.119.171",
                Port                 = 3389,
                Username             = "administrator",
                EncryptedPassword    = guacamole.ProtectPassword("DemoSalasana1!"),
                IgnoreCert           = true,
                Security             = "any",
                GuacamoleConnectionId = "1",
                Notes                = "HakaTech-kohdepalvelin Guacamolen kautta.",
                IsActive             = true,
                CustomerId           = techsol.Id
            }
        );
        await dbContext.SaveChangesAsync();

        // 10b. Tikettipalaute — vain suljetuille tiketeille pyydetään palautetta (1–5 tähteä)
        dbContext.TicketFeedbacks.AddRange(
            new TicketFeedback { TicketId = t3.Id, UserId = kalle.Id, Rating = 5, Comment = "Nopea ja asiantunteva palvelu, tulostin toimii taas!", SubmittedAt = DateTime.UtcNow.AddDays(-18) }
        );
        await dbContext.SaveChangesAsync();

        // 11. Tietopankki: Self-service ohjeet asiakkaille. Kategoriat ja artikkelit (HTML-sisältö).
        var kbYleinen    = new KnowledgeBaseCategory { Name = "Yleistä",          SortOrder = 1, IsActive = true };
        var kbLaskutus   = new KnowledgeBaseCategory { Name = "Laskutus",         SortOrder = 2, IsActive = true };
        var kbTekniset   = new KnowledgeBaseCategory { Name = "Tekniset ohjeet",  SortOrder = 3, IsActive = true };
        var kbTietoturva = new KnowledgeBaseCategory { Name = "Tietoturva",       SortOrder = 4, IsActive = true };

        dbContext.KnowledgeBaseCategories.AddRange(kbYleinen, kbLaskutus, kbTekniset, kbTietoturva);
        await dbContext.SaveChangesAsync();

        dbContext.KnowledgeBaseArticles.AddRange(
            new KnowledgeBaseArticle
            {
                Title      = "Miten luon tukitiketin?",
                CategoryId = kbYleinen.Id,
                IsFeatured = true,
                IsPublished = true,
                CreatedByUserId = admin1.Id,
                CreatedAt  = DateTime.UtcNow,
                UpdatedAt  = DateTime.UtcNow,
                Content    = """
                    <h2>Tiketin luominen portaalissa</h2>
                    <p>Tukitiketin luominen on nopeaa. Seuraa näitä vaiheita:</p>
                    <ol>
                        <li>Siirry <strong>Tiketit</strong>-osioon vasemmasta valikosta.</li>
                        <li>Klikkaa <strong>Uusi tiketti</strong> -painiketta.</li>
                        <li>Kirjoita lyhyt ja kuvaava otsikko.</li>
                        <li>Kuvaile ongelma mahdollisimman tarkasti: mitä tapahtui, milloin ja miten se voidaan toistaa.</li>
                        <li>Valitse sopiva <strong>kategoria</strong> ja <strong>prioriteetti</strong>.</li>
                        <li>Klikkaa <strong>Lähetä tiketti</strong>.</li>
                    </ol>
                    <p>Tukitiimimme vastaa arkisin 8–17 välillä. Kiireellisissä asioissa valitse prioriteetiksi <em>Kiireellinen</em>.</p>
                    <h3>Vinkkejä hyvään tikettiin</h3>
                    <ul>
                        <li>Liitä mukaan kuvakaappaukset tai virheilmoitukset.</li>
                        <li>Mainitse, monelle käyttäjälle ongelma vaikuttaa.</li>
                        <li>Kerro, onko ongelma toistuva vai kertaluonteinen.</li>
                    </ul>
                    """
            },
            new KnowledgeBaseArticle
            {
                Title      = "Laskun maksaminen ja eräpäivä",
                CategoryId = kbLaskutus.Id,
                IsFeatured = true,
                IsPublished = true,
                CreatedByUserId = admin1.Id,
                CreatedAt  = DateTime.UtcNow,
                UpdatedAt  = DateTime.UtcNow,
                Content    = """
                    <h2>Laskutuskäytännöt</h2>
                    <p>HakaTechin laskut lähetetään kuukauden lopussa sähköpostitse PDF-muodossa.</p>
                    <h3>Maksuaika</h3>
                    <p>Maksuaika on <strong>14 päivää</strong> laskun päiväyksestä. Eräpäivä näkyy laskun oikeassa yläkulmassa.</p>
                    <h3>Maksutavat</h3>
                    <ul>
                        <li>Verkkopankkisiirto (IBAN näkyy laskulla)</li>
                        <li>E-lasku — pyydä aktivointi tukitiimiltä</li>
                    </ul>
                    <h3>Myöhästynyt maksu</h3>
                    <p>Viivästymisestä peritään viivästyskorko lain mukaisesti. Maksamattomista laskuista lähetään muistutus 7 päivää eräpäivän jälkeen.</p>
                    <h3>Laskuun liittyvät kysymykset</h3>
                    <p>Jos laskussa on virhe tai tarvitset hyvityslaskun, ota yhteyttä laskutustiimiin tikettijärjestelmän kautta ja valitse kategoriaksi <em>Laskutus</em>.</p>
                    """
            },
            new KnowledgeBaseArticle
            {
                Title      = "VPN-yhteyden muodostaminen",
                CategoryId = kbTekniset.Id,
                IsFeatured = true,
                IsPublished = true,
                CreatedByUserId = admin1.Id,
                CreatedAt  = DateTime.UtcNow,
                UpdatedAt  = DateTime.UtcNow,
                Content    = """
                    <h2>VPN-yhteys HakaTechin verkkoon</h2>
                    <p>Tarvitset VPN-yhteyden päästäksesi asiakasverkon sisäisiin resursseihin.</p>
                    <h3>Vaatimukset</h3>
                    <ul>
                        <li>VPN-asiakasohjelmisto (toimitetaan onboardingin yhteydessä)</li>
                        <li>Henkilökohtainen VPN-sertifikaatti</li>
                        <li>Toimiva internet-yhteys</li>
                    </ul>
                    <h3>Yhteyden muodostaminen</h3>
                    <ol>
                        <li>Avaa VPN-asiakasohjelma.</li>
                        <li>Valitse profiiliksi <strong>HakaTech-Asiakkaat</strong>.</li>
                        <li>Syötä käyttäjätunnus (sama kuin portaalissa) ja salasana.</li>
                        <li>Klikkaa <strong>Yhdistä</strong>.</li>
                    </ol>
                    <h3>Yleisimmät ongelmat</h3>
                    <p><strong>Yhteys ei muodostu:</strong> Tarkista palomuuriasetukset. UDP-portin 1194 täytyy olla auki.</p>
                    <p><strong>Sertifikaatti vanhentunut:</strong> Pyydä uusi sertifikaatti tukitiketillä.</p>
                    """
            },
            new KnowledgeBaseArticle
            {
                Title      = "Salasanan vaihtaminen ja nollaaminen",
                CategoryId = kbTietoturva.Id,
                IsFeatured = true,
                IsPublished = true,
                CreatedByUserId = admin1.Id,
                CreatedAt  = DateTime.UtcNow,
                UpdatedAt  = DateTime.UtcNow,
                Content    = """
                    <h2>Salasanan hallinta</h2>
                    <h3>Salasanan vaihto portaalissa</h3>
                    <ol>
                        <li>Klikkaa profiilikuvakettasi sivupalkin alaosassa.</li>
                        <li>Valitse <strong>Vaihda salasana</strong> (avainikkoni).</li>
                        <li>Syötä nykyinen salasana ja uusi salasana kahdesti.</li>
                        <li>Klikkaa <strong>Tallenna</strong>.</li>
                    </ol>
                    <h3>Unohtunut salasana</h3>
                    <p>Jos olet unohtanut salasanasi, ota yhteyttä tukitiimiin. Salasanoja ei voida palauttaa, mutta admin voi asettaa uuden salasanan.</p>
                    <h3>Salasanavaatimukset</h3>
                    <ul>
                        <li>Vähintään 8 merkkiä</li>
                        <li>Vähintään yksi iso kirjain</li>
                        <li>Vähintään yksi numero tai erikoismerkki</li>
                    </ul>
                    <h3>Tietoturvaohjeet</h3>
                    <p>Älä jaa salasanaa kenellekään, myös tukihenkilöstö ei tarvitse sitä. Vaihda salasana säännöllisesti (suositus: 3 kuukauden välein).</p>
                    """
            },
            new KnowledgeBaseArticle
            {
                Title      = "Palvelutasosopimus (SLA) ja vasteajat",
                CategoryId = kbYleinen.Id,
                IsPublished = true,
                CreatedByUserId = admin1.Id,
                CreatedAt  = DateTime.UtcNow,
                UpdatedAt  = DateTime.UtcNow,
                Content    = """
                    <h2>Palvelutasosopimus</h2>
                    <p>HakaTechin palvelutasosopimus määrittelee vasteajat tiketin prioriteetin mukaan.</p>
                    <table class="table table-bordered table-sm mt-3">
                        <thead class="table-light">
                            <tr><th>Prioriteetti</th><th>Ensivaste</th><th>Ratkaisu</th></tr>
                        </thead>
                        <tbody>
                            <tr><td><span class="badge bg-danger">Kriittinen</span></td><td>1 tunti</td><td>4 tuntia</td></tr>
                            <tr><td><span class="badge bg-warning text-dark">Kiireellinen</span></td><td>4 tuntia</td><td>1 työpäivä</td></tr>
                            <tr><td><span class="badge bg-primary">Normaali</span></td><td>1 työpäivä</td><td>3 työpäivää</td></tr>
                            <tr><td><span class="badge bg-secondary">Matala</span></td><td>2 työpäivää</td><td>5 työpäivää</td></tr>
                        </tbody>
                    </table>
                    <p class="text-muted small">Vasteajat koskevat arkipäiviä klo 8–17. Kriittiset häiriöt 24/7.</p>
                    """
            },
            new KnowledgeBaseArticle
            {
                Title      = "Varmuuskopiointi ja tietojen palautus",
                CategoryId = kbTekniset.Id,
                IsPublished = true,
                CreatedByUserId = admin1.Id,
                CreatedAt  = DateTime.UtcNow,
                UpdatedAt  = DateTime.UtcNow,
                Content    = """
                    <h2>Varmuuskopiointikäytännöt</h2>
                    <p>HakaTech varmuuskopioi asiakkaiden tiedot automaattisesti seuraavasti:</p>
                    <ul>
                        <li><strong>Päivittäinen varmuuskopio</strong> – säilytetään 30 päivää</li>
                        <li><strong>Viikoittainen varmuuskopio</strong> – säilytetään 3 kuukautta</li>
                        <li><strong>Kuukausittainen varmuuskopio</strong> – säilytetään 1 vuosi</li>
                    </ul>
                    <h3>Tietojen palautus</h3>
                    <p>Pyydä palautusta tukitiketillä. Mainitse:</p>
                    <ol>
                        <li>Mitä tiedostoja tai tietokantoja haluat palauttaa</li>
                        <li>Mihin ajankohtaan palautetaan (päivämäärä ja kellonaika)</li>
                        <li>Palautetaanko alkuperäiseen sijaintiin vai väliaikaiseen kansioon</li>
                    </ol>
                    <p><strong>Huom:</strong> Palautuspyyntöihin vastataan seuraavana arkipäivänä. Kiireellisissä tapauksissa merkitse tiketti prioriteetiltaan <em>Kiireellinen</em>.</p>
                    """
            }
        );
        await dbContext.SaveChangesAsync();

        // 12. Huoltokalenteri: BookingSlot = vapaa aikaikkuna, Booking = asiakkaan varaus siihen.
        //     Luodaan sekä tulevia että yksi mennyt slotti, jotta historianäkymä toimii.
        var now = DateTime.Now;

        // Tulevia aikavälyjä
        var slot1 = new BookingSlot
        {
            Title           = "Palvelinhuolto ja päivitykset",
            SlotType        = BookingSlotType.Maintenance,
            StartTime       = now.Date.AddDays(3).AddHours(10),
            DurationMinutes = 120,
            MaxCapacity     = 1,
            IsActive        = true,
            CreatedByUserId = admin1.Id,
            Description     = "Käyttöjärjestelmäpäivitykset, varmuuskopioinnin tarkistus ja suorituskykyanalyysi."
        };
        var slot2 = new BookingSlot
        {
            Title           = "IT-konsultointi",
            SlotType        = BookingSlotType.Consulting,
            StartTime       = now.Date.AddDays(5).AddHours(13),
            DurationMinutes = 60,
            MaxCapacity     = 3,
            IsActive        = true,
            CreatedByUserId = admin2.Id,
            Description     = "Vapaamuotoinen konsultointitunti IT-strategiaan tai teknisiin kysymyksiin."
        };
        var slot3 = new BookingSlot
        {
            Title           = "Etätukisessio",
            SlotType        = BookingSlotType.RemoteSupport,
            StartTime       = now.Date.AddDays(2).AddHours(9),
            DurationMinutes = 45,
            MaxCapacity     = 1,
            IsActive        = true,
            CreatedByUserId = admin1.Id
        };
        var slot4 = new BookingSlot
        {
            Title           = "Verkon tarkistus ja optimointi",
            SlotType        = BookingSlotType.Maintenance,
            StartTime       = now.Date.AddDays(7).AddHours(8),
            DurationMinutes = 90,
            MaxCapacity     = 2,
            IsActive        = true,
            CreatedByUserId = admin2.Id,
            Description     = "Verkon suorituskyky, palomuurin tarkistus ja langattoman verkon optimointi."
        };
        var slot5 = new BookingSlot
        {
            Title           = "Microsoft 365 -käyttöönottokonsultointi",
            SlotType        = BookingSlotType.Consulting,
            StartTime       = now.Date.AddDays(10).AddHours(14),
            DurationMinutes = 90,
            MaxCapacity     = 5,
            IsActive        = true,
            CreatedByUserId = admin1.Id
        };
        // Mennyt aikaväli (historiaa varten)
        var slotPast = new BookingSlot
        {
            Title           = "Tulostimen huolto",
            SlotType        = BookingSlotType.Maintenance,
            StartTime       = now.Date.AddDays(-10).AddHours(11),
            DurationMinutes = 60,
            MaxCapacity     = 1,
            IsActive        = true,
            CreatedByUserId = admin2.Id
        };

        dbContext.BookingSlots.AddRange(slot1, slot2, slot3, slot4, slot5, slotPast);
        await dbContext.SaveChangesAsync();

        // Demo-varaukset
        dbContext.Bookings.AddRange(
            new Booking
            {
                BookingSlotId = slot2.Id,
                CustomerId    = digimolli.Id,
                UserId        = matti.Id,
                Status        = BookingStatus.Confirmed,
                Notes         = "Kysymyksiä M365-lisensoinnista ja Teams-integraatiosta.",
                CreatedAt     = DateTime.UtcNow.AddDays(-1)
            },
            new Booking
            {
                BookingSlotId = slot3.Id,
                CustomerId    = techsol.Id,
                UserId        = miia.Id,
                Status        = BookingStatus.Pending,
                Notes         = "VPN-yhteysongelma etätyöntekijöillä.",
                CreatedAt     = DateTime.UtcNow.AddHours(-3)
            },
            new Booking
            {
                BookingSlotId = slotPast.Id,
                CustomerId    = kivikangas.Id,
                UserId        = kalle.Id,
                Status        = BookingStatus.Confirmed,
                CreatedAt     = DateTime.UtcNow.AddDays(-11)
            }
        );
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Apufunktio uuden käyttäjän luomiseen. Luo Identity-käyttäjän,
    /// asettaa salasanan ja lisää käyttäjän annettuun rooliin.
    /// EmailConfirmed=true ohittaa sähköpostin vahvistuksen demo-dataa varten.
    /// </summary>
    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string fullName, int? customerId, string role, string password, bool isCustomerAdmin = false)
    {
        var user = new ApplicationUser
        {
            UserName        = email,
            Email           = email,
            FullName        = fullName,
            CustomerId      = customerId,
            IsCustomerAdmin = isCustomerAdmin,
            EmailConfirmed  = true   // ei vaadi sähköpostin vahvistusta seedauksessa
        };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, role);
        return user;
    }
}
