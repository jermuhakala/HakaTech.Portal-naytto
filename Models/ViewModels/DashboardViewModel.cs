// Domain-mallien tyypit — Ticket, Invoice, BookingSlot, Announcement, TicketStatus.
using HakaTech.Portal.Models.Domain;

// Nimiavaruus.
namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Dashboardin (etusivun) malli. Sisältää kaikki tilastot, listaukset
/// ja widget-data, joita käyttäjälle näytetään. Sisältö riippuu roolista:
///   - Admin: kaikki asiakkaat ja kokonaisluvut
///   - Asiakas: vain oman yrityksen luvut
/// </summary>
// Dashboard on portaalin "kaikkien kauppa": yksi ViewModel sisältää kaiken mitä etusivu tarvitsee.
// HomeController rakentaa tämän kokonaan ja antaa sen Index.cshtml-näkymälle.
// Malli on suuri mutta tarkoituksellinen — se korvaa kymmenet erilliset ViewBag-kentät.
public class DashboardViewModel
{
    /// <summary>Käyttäjän mukautettu widgettien järjestys (avain-listan järjestys).</summary>
    // Lista widget-avaimista siinä järjestyksessä kun käyttäjä on ne järjestänyt.
    // Esim. ["tickets", "kpi", "invoices", "calendar", "quickactions"].
    // Tallennetaan tietokantaan pilkkueroteltuna merkkijonona (ApplicationUser.DashboardLayout).
    public List<string> WidgetOrder { get; set; } = [];

    /// <summary>Lähestyvät kalenterivaraukset (huoltokalenteri).</summary>
    // Seuraavien 14 päivän aikaikkunat. Näytetään kalenteriwidgetissä.
    public IList<BookingSlot> UpcomingBookingSlots { get; set; } = [];

    // ── Yleiset tilastot ───────────────────────────────────────────────────────
    // Adminille koko järjestelmä, asiakkaalle vain oman yrityksen luvut.
    // Nämä täytetään HomeController.Index()-metodissa kantakyselyistä.

    // Asiakastilastot (vain admin).
    public int TotalCustomers     { get; set; }  // Asiakkaita yhteensä.
    public int ActiveCustomers    { get; set; }  // Aktiivisia asiakkaita.

    // Tikettitilastot.
    public int TotalTickets       { get; set; }  // Tikettejä yhteensä.
    public int OpenTickets        { get; set; }  // Avoimia tikettejä.
    public int InProgressTickets  { get; set; }  // Tikettejä käsittelyssä.
    public int ResolvedTickets    { get; set; }  // Ratkaistuja tikettejä.

    // Laskutilastot.
    public int TotalInvoices      { get; set; }  // Laskuja yhteensä.

    /// <summary>Maksamatta yhteensä euroissa.</summary>
    // decimal = tarkka laskenta rahalle. Lähestyvät ja erääntyneet laskut yhteensä.
    public decimal UnpaidTotal    { get; set; }

    /// <summary>Erääntyneiden laskujen määrä (eräpäivä mennyt, ei maksettu).</summary>
    public int OverdueCount       { get; set; }

    /// <summary>Kriittisten avoimien tikettien määrä — vaatii välitöntä reagointia.</summary>
    // Kriittiset tiketit korostetaan punaisella dashboard-kortissa.
    public int CriticalTickets    { get; set; }

    /// <summary>Korkean prioriteetin avoimien tikettien määrä.</summary>
    public int HighTickets        { get; set; }

    /// <summary>5 viimeisintä avointa tikettiä taulukkoa varten.</summary>
    // IList<> eikä List<> — rajapintatyyppi, jolla ei ole väliä onko se List, Array tms.
    public IList<Ticket> RecentOpenTickets { get; set; } = [];

    /// <summary>Lähestyvät eräpäivät — laskut, joiden eräpäivä 7 päivän sisällä.</summary>
    public IList<Invoice> UpcomingDueInvoices { get; set; } = [];

    /// <summary>Jo erääntyneet laskut.</summary>
    public IList<Invoice> OverdueInvoices { get; set; } = [];

    /// <summary>Tikettien tilajakauma piirakkadiagrammia varten (Status → kpl).</summary>
    // Dictionary = avain-arvo-pari. Avain on tila (TicketStatus), arvo on kappalemäärä.
    // Esim. {Open: 12, InProgress: 5, Closed: 48}.
    // Chart.js rakentaa tästä piirakkakaavion automaattisesti.
    public Dictionary<TicketStatus, int> TicketsByStatus { get; set; } = new();

    /// <summary>Aktiiviset tiedotteet (esim. huoltokatkot), näytetään etusivulla.</summary>
    public IList<Announcement> ActiveAnnouncements { get; set; } = [];

    /// <summary>Adminille: käsittelyä odottavien tarjouspyyntöjen määrä.</summary>
    // Näytetään adminille "badge"-ilmoituksena (punainen numero).
    public int PendingQuoteRequests { get; set; }

    /// <summary>Asiakkaalle: uudet (ei vielä avatut) lähetetyt laskut.</summary>
    // Asiakkaalle näytetään "Sinulla on X uutta laskua" -ilmoitus.
    public IList<Invoice> NewSentInvoices { get; set; } = [];

    /// <summary>Onko käyttäjä admin (ohjaa näytettäviä widgeteitä).</summary>
    // Razor-näkymässä tarkistetaan tätä: @if (Model.IsAdmin) { ... }
    public bool IsAdmin { get; set; }

    /// <summary>Tervehdysteksti kortin yläosassa, esim. "Tervetuloa, Matti!".</summary>
    // Täytetään käyttäjän nimellä tai sähköpostilla.
    public string WelcomeName { get; set; } = string.Empty;

    // ── Sparkline-trendit (14 päivää, uusin viimeisenä) ────────────────────────
    // Sparkline = pieni viivakaavio kortin nurkassa, kuten Linear-tyylissä.
    // double[] = taulukko (array) desimaaliluvuista. Indeksi 0 = vanhin, 13 = uusin.
    // Chart.js piirtää nämä taulukon perusteella automaattisesti.

    /// <summary>Päivittäin luotujen tikettien määrä viimeiseltä 14 päivältä.</summary>
    // Esim. [2, 0, 5, 1, 3, ...] = montako tikettiä luotiin per päivä.
    public double[] TicketsCreatedSpark { get; set; } = [];

    /// <summary>Avoimien tikettien määrä päivittäin viimeiseltä 14 päivältä.</summary>
    public double[] TicketsOpenSpark    { get; set; } = [];

    /// <summary>Päivittäin luotujen laskujen määrä.</summary>
    public double[] InvoicesIssuedSpark { get; set; } = [];

    /// <summary>Maksamattomien laskujen kokonaissumman trendi.</summary>
    // Esim. [1000.00, 1500.50, 2000.00, ...] — kasvaa kun uusia laskuja luodaan.
    public double[] UnpaidTotalSpark    { get; set; } = [];
}
