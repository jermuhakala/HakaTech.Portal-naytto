# HakaTech Portal

B2B-asiakastukiportaali IT-palveluyritykselle ja sen yritysasiakkaille. Yhdistää tikettijärjestelmän, laskutuksen, palvelusopimukset, ajanvarauksen, tietämyskannan, etätyöpöytäyhteydet ja palvelukatalogin yhteen sovellukseen.

## Teknologia

- ASP.NET Core 9.0 MVC (Razor Views)
- Entity Framework Core 9 + SQL Server LocalDB
- ASP.NET Core Identity (rooli- ja käyttäjähallinta)
- SignalR — reaaliaikainen tikettikeskustelu
- QuestPDF — laskujen ja raporttien PDF-generointi
- Bootstrap 5 + omat design-tokenit
- Lokalisointi: suomi / ruotsi / englanti

## Vaatimukset

- Visual Studio 2022 (17.8 tai uudempi) **tai** .NET 9 SDK + IDE valinnan mukaan
- SQL Server LocalDB (asentuu Visual Studion mukana)
- Windows (LocalDB-vaatimuksen takia; muilla alustoilla connection string vaihdettava)

## Käynnistys

1. Avaa `HakaTech.Portal.sln` Visual Studiossa
2. Käynnistä **F5**:llä tai komentoriviltä:
   ```bash
   dotnet restore
   dotnet run --profile https
   ```
3. Selain avautuu osoitteeseen `https://localhost:7102`
4. Tietokanta ja demo-data luodaan automaattisesti ensimmäisellä ajolla

Jos selain valittaa kehityssertifikaatista, aja kerran:
```bash
dotnet dev-certs https --trust
```

## Demo-tunnukset

Salasana kaikille: `HakaTech2025!`

| Rooli | Sähköposti |
|-------|------------|
| Admin (järjestelmänvalvoja) | `admin@hakatech.fi` |
| Admin (asiakastuki) | `support@hakatech.fi` |
| Asiakas — DigiMölli (pääkäyttäjä) | `matti@digimolli.fi` |
| Asiakas — DigiMölli | `laura@digimolli.fi` |
| Asiakas — Kivikangas Rakennus | `kalle@kivikangas.fi` |
| Asiakas — TechSolutions Finland | `miia@techsolutions.fi` |

## Arkkitehtuuri

```
Controllers/        MVC-kontrollerit, roolipohjainen [Authorize]
Models/Domain/      EF Core -entiteetit
Models/ViewModels/  Näkymäkohtaiset DTO:t
Views/              Razor-näkymät
Services/           Rajapintapohjaiset palvelut (DI)
Data/               ApplicationDbContext + SeedData
Hubs/               SignalR (TicketHub → /hubs/ticket)
Migrations/         EF Core -migraatiot
Resources/          Lokalisointiresurssit (.resx)
```

**Keskeiset domain-suhteet:**
- `Customer` on keskusentiteetti — käyttäjät, tiketit, laskut ja sopimukset kuuluvat aina yhdelle asiakkaalle
- `ApplicationUser` periytyy `IdentityUser`:sta, sisältää `CustomerId`-FK:n (null = admin)
- `Ticket` ↔ `TicketComment` (sis. `IsInternal`-lipun admin-kommenteille) ja `TicketAttachment`
- `Invoice` ↔ `InvoiceLine` ja `InvoiceAttachment`

## Tietoturva

- Cookie-autentikointi, 8 h liukuva voimassaolo
- Käyttäjätilin lukitus: 5 epäonnistunutta yritystä → 15 min lukko
- Antiforgery-tokenit kaikissa POST-formeissa
- HTML-syöte sanitoidaan (`HtmlSanitizer`) ennen tallennusta
- Tiedostolatausten päätetarkistus + tallennus `wwwroot/uploads/`-kansioon palvelurajapinnan kautta
- Rate limiting kirjautumis- ja rekisteröintipäätepisteille (10 pyyntöä / IP / min)
- Content-Security-Policy, X-Frame-Options, HSTS-otsikot
- Salasanavaatimukset: vähintään 10 merkkiä, iso/pieni/numero/erikoismerkki

## Huomioita näyttöä / arviointia varten

**Tietokanta nollautuu kun se on tyhjä.** Migraatiot ajetaan automaattisesti ja `SeedData` luo demo-datan, jos `Customers`-taulu on tyhjä. Jos haluat puhtaan ympäristön, poista `HakaTechPortal`-kanta SQL Server Object Explorerista ja käynnistä sovellus uudelleen.

**Sähköposti (SMTP) ei ole konfiguroitu** — `appsettings.json`:n `SmtpSettings` on tyhjä. `SmtpEmailService` kirjaa lähetysvirheet lokiin ja jatkaa normaalisti, joten sovellus toimii ilman SMTP-palvelinta. Mailit eivät lähde, mutta käyttäjäpolut eivät keskeydy.

**Apache Guacamole -etätyöpöytä** vaatii erillisen Guacamole-palvelimen ja sen tunnukset (`GuacamoleSettings`-osio). Demoympäristössä yhteyksien hallinta-UI (luo, listaa, muokkaa) toimii normaalisti, mutta varsinainen etäyhteys ei avaudu ilman taustapalvelinta. Tämä on tarkoituksellinen integraatiopiste, ei puutteellinen toiminto.

## EF Core -migraatiot

Tarvitaan vain jos muokkaat domain-malleja:

```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

Sovellus ajaa puuttuvat migraatiot automaattisesti käynnistyksen yhteydessä (`Database.MigrateAsync()` `SeedData`:ssa).

## Lisenssi

Opetuskäyttöön. QuestPDF käyttää Community-lisenssiä (`Program.cs`).
