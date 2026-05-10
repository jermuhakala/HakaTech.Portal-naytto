using HakaTech.Portal.Models.Domain;

namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Dashboardin (etusivun) malli. Sisältää kaikki tilastot, listaukset
/// ja widget-data, joita käyttäjälle näytetään. Sisältö riippuu roolista:
///   - Admin: kaikki asiakkaat ja kokonaisluvut
///   - Asiakas: vain oman yrityksen luvut
/// </summary>
public class DashboardViewModel
{
    /// <summary>Käyttäjän mukautettu widgettien järjestys (avain-listan järjestys).</summary>
    public List<string> WidgetOrder { get; set; } = [];

    /// <summary>Lähestyvät kalenterivaraukset (huoltokalenteri).</summary>
    public IList<BookingSlot> UpcomingBookingSlots { get; set; } = [];

    // ── Yleiset tilastot ─────────────────────────────────────────
    // Adminille koko järjestelmä, asiakkaalle vain oma yritys.

    public int TotalCustomers     { get; set; }
    public int ActiveCustomers    { get; set; }

    public int TotalTickets       { get; set; }
    public int OpenTickets        { get; set; }
    public int InProgressTickets  { get; set; }
    public int ResolvedTickets    { get; set; }

    public int TotalInvoices      { get; set; }

    /// <summary>Maksamatta yhteensä euroissa.</summary>
    public decimal UnpaidTotal    { get; set; }

    /// <summary>Erääntyneiden laskujen määrä (eräpäivä mennyt, ei maksettu).</summary>
    public int OverdueCount       { get; set; }

    /// <summary>Kriittisten avoimien tikettien määrä — vaatii välitöntä reagointia.</summary>
    public int CriticalTickets    { get; set; }

    /// <summary>Korkean prioriteetin avoimien tikettien määrä.</summary>
    public int HighTickets        { get; set; }

    /// <summary>5 viimeisintä avointa tikettiä taulukkoa varten.</summary>
    public IList<Ticket> RecentOpenTickets { get; set; } = [];

    /// <summary>Lähestyvät eräpäivät — laskut, joiden eräpäivä 7 päivän sisällä.</summary>
    public IList<Invoice> UpcomingDueInvoices { get; set; } = [];

    /// <summary>Jo erääntyneet laskut.</summary>
    public IList<Invoice> OverdueInvoices { get; set; } = [];

    /// <summary>Tikettien tilajakauma piirakkadiagrammia varten (Status → kpl).</summary>
    public Dictionary<TicketStatus, int> TicketsByStatus { get; set; } = new();

    /// <summary>Aktiiviset tiedotteet (esim. huoltokatkot), näytetään etusivulla.</summary>
    public IList<Announcement> ActiveAnnouncements { get; set; } = [];

    /// <summary>Adminille: käsittelyä odottavien tarjouspyyntöjen määrä.</summary>
    public int PendingQuoteRequests { get; set; }

    /// <summary>Asiakkaalle: uudet (ei vielä avatut) lähetetyt laskut.</summary>
    public IList<Invoice> NewSentInvoices { get; set; } = [];

    /// <summary>Onko käyttäjä admin (ohjaa näytettäviä widgeteitä).</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Tervehdysteksti kortin yläosassa, esim. "Tervetuloa, Matti!".</summary>
    public string WelcomeName { get; set; } = string.Empty;

    // ── Sparkline-trendit (14 päivää, uusin viimeisenä) ──────────
    // Pieni minimaalinen viivakaavio kortin nurkassa Linear-tyyliin.

    /// <summary>Päivittäin luotujen tikettien määrä viimeiseltä 14 päivältä.</summary>
    public double[] TicketsCreatedSpark { get; set; } = [];

    /// <summary>Avoimien tikettien määrä päivittäin viimeiseltä 14 päivältä.</summary>
    public double[] TicketsOpenSpark    { get; set; } = [];

    /// <summary>Päivittäin luotujen laskujen määrä.</summary>
    public double[] InvoicesIssuedSpark { get; set; } = [];

    /// <summary>Maksamattomien laskujen kokonaissumman trendi.</summary>
    public double[] UnpaidTotalSpark    { get; set; } = [];
}
