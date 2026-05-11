// Nimiavaruuksien tuonnit.
using System.Globalization;                  // CultureInfo, ISOWeek — suomenkieliset päivämäärät ja viikonumerot.
using System.Text;                           // StringBuilder — tehokas CSV-merkkijonon rakentaminen.
using HakaTech.Portal.Data;                  // ApplicationDbContext — tietokantayhteys.
using HakaTech.Portal.Models.Domain;         // TicketStatus, InvoiceStatus — enum-arvot.
using HakaTech.Portal.Models.ViewModels;     // ReportIndexViewModel, WeeklyTicketStat, MonthlyBillingStat.
using HakaTech.Portal.Services;             // BillingReportPdfDocument — PDF-raporttiluokka.
using Microsoft.AspNetCore.Authorization;   // [Authorize]-attribuutti.
using Microsoft.AspNetCore.Mvc;             // Controller, IActionResult, FileContentResult...
using Microsoft.EntityFrameworkCore;        // GroupBy, ToListAsync, Select...

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Raporttien controller — vain admin. Tarjoaa viikoittaiset
/// tikettiluvut, kuukausittaiset laskutusluvut sekä CSV- ja PDF-viennit.
/// </summary>
// [Authorize(Roles = "Admin")] = koko controller on rajoitettu adminille.
// Asiakaskäyttäjä ei näe raportteja — ne sisältävät kaikkien yritysten tietoja.
[Authorize(Roles = "Admin")]
public class ReportController : Controller
{
    // Tietokantayhteys.
    private readonly ApplicationDbContext _db;

    // Konstruktori: DI-säiliö täyttää parametrin.
    public ReportController(ApplicationDbContext db)
    {
        _db = db;
    }

    // Suomenkielinen kulttuuriasetus — käytetään päivämäärien ja lukujen muotoiluun.
    // "static readonly" = luodaan vain kerran koko sovelluksen elinkaaren aikana.
    private static readonly CultureInfo FI = new("fi-FI");

    // ── GET /Report ──────────────────────────────────────────────────────────
    // Raporttien pääsivu: viikoittaiset tiketit, kuukausittainen laskutus,
    // tilajakauma piirakkakaaviolle ja palautekeskiarvo.
    public async Task<IActionResult> Index(
        int weeksBack  = 12, // Montako viikkoa taaksepäin tikettiraportti näyttää. Oletuksena 12.
        int monthsBack = 12) // Montako kuukautta taaksepäin laskutusraportti näyttää. Oletuksena 12.
    {
        // Rajoitetaan parametrit järkeviin arvoihin.
        // Liian pieni tai suuri arvo voisi tehdä raporteista käyttökelvottomia tai hitaita.
        weeksBack  = Math.Clamp(weeksBack,  4, 52); // Vähintään 4 viikkoa, enintään vuosi.
        monthsBack = Math.Clamp(monthsBack, 3, 24); // Vähintään 3 kuukautta, enintään 2 vuotta.

        // Rakennetaan ViewModel — täytetään alla eri tilastoilla.
        var vm = new ReportIndexViewModel
        {
            WeeksBack  = weeksBack,
            MonthsBack = monthsBack,
            // Rakennetaan viikoittaiset tilastot apumetodilla.
            WeeklyStats  = await BuildWeeklyStats(weeksBack),
            // Rakennetaan kuukausittaiset laskutustilastot apumetodilla.
            MonthlyStats = await BuildMonthlyStats(monthsBack)
        };

        // ── Tilajakauma ──────────────────────────────────────────────────────
        // Lasketaan tikettien määrät tiloittain piirakkakaaviolle.
        // GroupBy(t.Status) = SQL:n GROUP BY Status.
        // Select(g => new { ... }) = muunnetaan ryhmät anonyymiksi olioiksi.
        var counts = await _db.Tickets
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        // switch-lause: täytetään ViewModel:n kentät tilaryhmien mukaan.
        // "switch" on tehokkaampi kuin if-else-if ketju kun on monta haaraa.
        foreach (var c in counts)
        {
            switch (c.Status)
            {
                case TicketStatus.Open:            vm.CountOpen            = c.Count; break;
                case TicketStatus.InProgress:      vm.CountInProgress      = c.Count; break;
                case TicketStatus.WaitingCustomer: vm.CountWaitingCustomer = c.Count; break;
                case TicketStatus.Resolved:        vm.CountResolved        = c.Count; break;
                case TicketStatus.Closed:          vm.CountClosed          = c.Count; break;
                // Muita tiloja ei ole — default-haara ei tarvita.
            }
        }

        // Yhteissummat laskutusraporttiin.
        // Sum() LINQ-metodi laskee kaikki MonthlyStats-listan arvot yhteen.
        vm.TotalBilledExcl = vm.MonthlyStats.Sum(s => s.TotalExcl); // Veroton yhteensä.
        vm.TotalBilledIncl = vm.MonthlyStats.Sum(s => s.TotalIncl); // Verollinen yhteensä.
        vm.TotalPaid       = vm.MonthlyStats.Sum(s => s.PaidAmount); // Maksettu yhteensä.

        // ── Palautekeskiarvo ─────────────────────────────────────────────────
        // Ladataan kaikki palautteet muistiin — ei tarvita navigaatio-ominaisuuksia.
        var feedbacks = await _db.TicketFeedbacks.ToListAsync();
        vm.FeedbackCount = feedbacks.Count;
        // Average() laskee aritmeettisen keskiarvon.
        // Ternary: jos palautteita ei ole, käytetään null (ei näytetä "0,0").
        // double? = nullable double — null tarkoittaa "ei arvoa".
        vm.AvgFeedbackRating = feedbacks.Count > 0
            ? feedbacks.Average(f => (double)f.Rating) // (double) = muunnetaan int → double laskentaa varten.
            : null;

        return View(vm);
    }

    // ── GET /Report/ExportTicketsCsv ─────────────────────────────────────────
    // Vie kaikki tiketit CSV-tiedostona (puolipisteerotteinen, Excel-yhteensopiva).
    [HttpGet]
    public async Task<IActionResult> ExportTicketsCsv()
    {
        // Haetaan kaikki tiketit tarvittavilla suhteilla.
        var tickets = await _db.Tickets
            .Include(t => t.Customer)       // Yrityksen nimi CSV:hen.
            .Include(t => t.CreatedByUser)  // Luojan sähköposti.
            .Include(t => t.AssignedToUser) // Vastuuhenkilön sähköposti.
            .OrderByDescending(t => t.CreatedAt) // Uusin ensin.
            .ToListAsync();

        // StringBuilder on tehokkaampi kuin string-yhdistely silmukassa.
        // Jokainen += luo uuden merkkijonon; StringBuilder muokkaa yhteistä puskuria.
        var sb = new StringBuilder();
        // Otsikkorivi — puolipiste on eurooppalaisissa Excel-asennuksissa oletuserotin.
        sb.AppendLine("Id;Otsikko;Tila;Prioriteetti;Kategoria;Asiakas;Luonut;Vastuuhenkilö;Luotu;Ratkaistu");

        foreach (var t in tickets)
        {
            // string.Join(";", ...) yhdistää arvot puolipisteillä yhdeksi riviksi.
            sb.AppendLine(string.Join(";",
                t.Id,
                Q(t.Title),            // Q() suojaa CSV-arvoissa olevat puolipisteet ja lainausmerkit.
                t.Status,              // Enum → teksti automaattisesti (esim. "Open").
                t.Priority,
                t.Category,
                Q(t.Customer?.CompanyName ?? ""),         // "??" = null → tyhjä merkkijono.
                Q(t.CreatedByUser?.Email ?? ""),
                Q(t.AssignedToUser?.Email ?? ""),
                t.CreatedAt.ToString("dd.MM.yyyy", FI),  // Suomalainen päivämääräformaatti.
                t.ResolvedAt?.ToString("dd.MM.yyyy", FI) ?? "" // Null → tyhjä (ei vielä ratkaistu).
            ));
        }

        // Palautetaan CSV-tiedosto — CsvFile on yksityinen apumetodi.
        return CsvFile(sb, $"tiketit_{DateTime.Today:yyyyMMdd}.csv");
    }

    // ── GET /Report/ExportBillingCsv ─────────────────────────────────────────
    // Vie kaikki laskut CSV-tiedostona.
    [HttpGet]
    public async Task<IActionResult> ExportBillingCsv()
    {
        // Haetaan kaikki laskut riveineen — rivit tarvitaan SubTotal/VatAmount/TotalAmount-laskentaan.
        var invoices = await _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Lines) // Lasketut ominaisuudet (SubTotal jne.) tarvitsevat rivit.
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Laskunro;Asiakas;Tila;Laskun pvm;Eräpäivä;Maksettu;Veroton (€);ALV (€);Yhteensä (€)");

        foreach (var inv in invoices)
        {
            sb.AppendLine(string.Join(";",
                inv.InvoiceNumber,
                Q(inv.Customer?.CompanyName ?? ""),
                inv.Status,
                inv.InvoiceDate.ToString("dd.MM.yyyy", FI),
                inv.DueDate.ToString("dd.MM.yyyy", FI),
                inv.PaidAt?.ToString("dd.MM.yyyy", FI) ?? "", // Maksupäivä tai tyhjä.
                // "F2" = kaksi desimaalia, pilkku erottimena (fi-FI: "1234,56").
                // Tärkeää: suomalaisessa CSV:ssä desimaalerottimet ovat pilkkuja.
                inv.SubTotal.ToString("F2", FI),
                inv.VatAmount.ToString("F2", FI),
                inv.TotalAmount.ToString("F2", FI)
            ));
        }

        return CsvFile(sb, $"laskutus_{DateTime.Today:yyyyMMdd}.csv");
    }

    // ── GET /Report/ExportBillingPdf ─────────────────────────────────────────
    // Vie laskutusraportin PDF-tiedostona.
    [HttpGet]
    public async Task<IActionResult> ExportBillingPdf(
        int monthsBack = 12) // Montako kuukautta raportissa näytetään.
    {
        monthsBack = Math.Clamp(monthsBack, 1, 24);
        // Rakennetaan kuukausittaiset tilastot (sama kuin Index-metodissa).
        var stats = await BuildMonthlyStats(monthsBack);

        // Lasketaan raportin aikavälin alku kuukauden tarkkuudella.
        // AddMonths(-(monthsBack - 1)) = esim. 12 kuukautta takaisin: nykyinen - 11 kuukautta.
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
            .AddMonths(-(monthsBack - 1));
        // Raportin otsikkoon tuleva aikavälimerkkijono: esim. "tammikuu 2024 – tammikuu 2025".
        string period = $"{monthStart.ToString("MMMM yyyy", FI)} – {DateTime.Now.ToString("MMMM yyyy", FI)}";

        // Luodaan PDF-dokumentti QuestPDF-kirjastolla.
        // BillingReportPdfDocument on erillinen luokka joka määrittelee raportin ulkoasun.
        var doc      = new BillingReportPdfDocument(stats, period);
        var pdfBytes = QuestPDF.Fluent.GenerateExtensions.GeneratePdf(doc);

        // Palautetaan PDF ladattavana tiedostona.
        return File(pdfBytes, "application/pdf", $"laskutusraportti_{DateTime.Today:yyyyMMdd}.pdf");
    }

    // ── Yksityiset apumetodit ────────────────────────────────────────────────

    // Rakentaa viikoittaiset tikettitilastot annetulta ajanjaksolta.
    private async Task<List<WeeklyTicketStat>> BuildWeeklyStats(int weeksBack)
    {
        // Jakson alku: weeksBack viikkoa sitten.
        var start = DateTime.UtcNow.Date.AddDays(-(weeksBack * 7));

        // Haetaan kaikki tiketit jotka on luotu TAI ratkaistu ajanjaksolla.
        // Ladataan vain tarvittavat kentät muistiin (Select(new {...})).
        var tickets = await _db.Tickets
            .Where(t => t.CreatedAt >= start ||
                        (t.ResolvedAt.HasValue && t.ResolvedAt >= start))
            .Select(t => new { t.CreatedAt, t.ResolvedAt })
            .ToListAsync();

        var stats = new List<WeeklyTicketStat>();
        // Iteraattorimuuttuja — kulkee viikko kerrallaan alusta loppuun.
        var cur = start;

        while (cur <= DateTime.UtcNow.Date)
        {
            var end = cur.AddDays(7); // Viikon loppu (ei sisältyvä: käytetään < end).
            // ISOWeek.GetWeekOfYear() = ISO 8601 -standardin mukainen viikonumero.
            // Esim. 2025-01-01 = viikko 1/2025 (ISO-standardi — eroaa .NET default-laskennasta).
            int weekNum = ISOWeek.GetWeekOfYear(cur);
            stats.Add(new WeeklyTicketStat
            {
                Label    = $"Vk {weekNum} / {cur.Year}", // Kaavion vaaka-akselin merkintä.
                // Luodut: tiketit joiden CreatedAt on tällä viikolla.
                Created  = tickets.Count(t => t.CreatedAt >= cur && t.CreatedAt < end),
                // Ratkaistut: tiketit joiden ResolvedAt on tällä viikolla.
                // HasValue = tarkistetaan ettei ole null ennen vertailua.
                Resolved = tickets.Count(t => t.ResolvedAt.HasValue
                                           && t.ResolvedAt >= cur
                                           && t.ResolvedAt < end)
            });
            cur = end; // Seuraava iteraatio alkaa siitä mihin edellinen loppui.
        }

        return stats;
    }

    // Rakentaa kuukausittaiset laskutustilastot annetulta ajanjaksolta.
    private async Task<List<MonthlyBillingStat>> BuildMonthlyStats(int monthsBack)
    {
        // Jakson alku: monthsBack kuukautta sitten, aina kuukauden 1. päivä.
        // "- (monthsBack - 1)" = esim. 12 → 11 kuukautta taaksepäin nykyisestä kuukaudesta.
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
            .AddMonths(-(monthsBack - 1));

        // Haetaan kaikki laskut ajanjaksolta riveineen (SubTotal/VatAmount/TotalAmount-laskentaa varten).
        var invoices = await _db.Invoices
            .Include(i => i.Lines)
            .Where(i => i.InvoiceDate >= monthStart)
            .ToListAsync();

        var stats = new List<MonthlyBillingStat>();
        var d = monthStart; // Kuukausi-iteraattori.

        // Silmukka käy läpi kuukauden kerrallaan nykyiseen kuukauteen asti.
        while (d.Year < DateTime.UtcNow.Year
            || (d.Year == DateTime.UtcNow.Year && d.Month <= DateTime.UtcNow.Month))
        {
            // Suodatetaan kyseisen kuukauden laskut muistilistasta.
            var month = invoices
                .Where(i => i.InvoiceDate.Year == d.Year && i.InvoiceDate.Month == d.Month)
                .ToList();

            stats.Add(new MonthlyBillingStat
            {
                // "MMMM yyyy" = koko kuukauden nimi + vuosi suomeksi (esim. "tammikuu 2025").
                Label        = d.ToString("MMMM yyyy", FI),
                InvoiceCount = month.Count,               // Laskujen lukumäärä kuukaudessa.
                TotalExcl    = month.Sum(i => i.SubTotal),    // Veroton summa yhteensä.
                TotalVat     = month.Sum(i => i.VatAmount),   // ALV-osuus yhteensä.
                TotalIncl    = month.Sum(i => i.TotalAmount), // Verollinen summa yhteensä.
                // Maksettujen laskujen summa — suodatetaan ensin Paid-tilaan.
                PaidAmount   = month
                    .Where(i => i.Status == InvoiceStatus.Paid)
                    .Sum(i => i.TotalAmount)
            });

            d = d.AddMonths(1); // Seuraava kuukausi.
        }

        return stats;
    }

    // Paketoi StringBuilder:in sisällön CSV-HTTP-vastaukseksi.
    private static FileContentResult CsvFile(StringBuilder sb, string fileName)
    {
        // UTF-8 BOM (Byte Order Mark) — Excel tunnistaa tiedoston enkoodauksen oikein.
        // Ilman BOM:ia skandinaaviset kirjaimet (ä, ö) voivat näkyä väärin Excelissä.
        // GetPreamble() palauttaa BOM-tavut (EF BB BF).
        // Concat() yhdistää BOM + CSV-tiedoston tavut yhdeksi taulukoksi.
        // ToArray() muuntaa listan taulukoksi.
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();

        // FileContentResult = HTTP-vastaus tiedoston sisällöllä.
        // "text/csv;charset=utf-8" = MIME-tyyppi — kertoo selaimelle tiedostomuodon.
        return new FileContentResult(bytes, "text/csv;charset=utf-8")
        {
            // FileDownloadName = Content-Disposition-otsikko — ehdotettu tiedostonimi latauksen yhteydessä.
            FileDownloadName = fileName
        };
    }

    // Suojaa CSV-arvon lainausmerkeillä — estää puolipisteiden rikkumasta CSV-rakennetta.
    // "private static" = ei tarvita instanssia, puhdas funktio.
    // "Q" = "quote" (lainausmerkkisuojaus).
    private static string Q(string value) =>
        // "\"" = yksi lainausmerkki (C# escape-sekvenssi).
        // value.Replace("\"", "\"\"") = sisäiset lainausmerkit kaksinkertaistetaan CSV-standardin mukaan.
        // Esim. arvo: Yritys "Abc" Ltd → CSV: "Yritys ""Abc"" Ltd"
        "\"" + value.Replace("\"", "\"\"") + "\"";
}
