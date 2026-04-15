using HakaTech.Portal.Models.Domain;

namespace HakaTech.Portal.Models.ViewModels;

public class DashboardViewModel
{
    // ── Mukautettu koontinäyttö ──────────────────────────────────────
    public List<string> WidgetOrder { get; set; } = [];

    // ── Tulevat varaukset (kalenteriwidget) ──────────────────────────
    public IList<BookingSlot> UpcomingBookingSlots { get; set; } = [];
    // ── Yleiset tilastot (Admin: kaikki, Asiakas: oma) ───────────────
    public int TotalCustomers     { get; set; }
    public int ActiveCustomers    { get; set; }

    public int TotalTickets       { get; set; }
    public int OpenTickets        { get; set; }
    public int InProgressTickets  { get; set; }
    public int ResolvedTickets    { get; set; }

    public int TotalInvoices      { get; set; }
    public decimal UnpaidTotal    { get; set; }  // Maksamatta yhteensä €
    public int OverdueCount       { get; set; }  // Erääntyneet laskut

    public int CriticalTickets    { get; set; }  // Kriittiset avoimet tiketit
    public int HighTickets        { get; set; }

    // ── Viimeisimmät avoimet tiketit ────────────────────────────────
    public IList<Ticket> RecentOpenTickets { get; set; } = [];

    // ── Lähestyvät eräpäivät (7 pv) ─────────────────────────────────
    public IList<Invoice> UpcomingDueInvoices { get; set; } = [];

    // ── Erääntyneet laskut ───────────────────────────────────────────
    public IList<Invoice> OverdueInvoices { get; set; } = [];

    // ── Tikettien tilajakauma (piirakkaa varten) ─────────────────────
    public Dictionary<TicketStatus, int> TicketsByStatus { get; set; } = new();

    // ── Tiedotteet & huoltoikkunat ───────────────────────────────────
    public IList<Announcement> ActiveAnnouncements { get; set; } = [];

    // ── Ilmoitukset (admin: odottavat tarjouspyynnöt, asiakas: uudet laskut) ──
    public int PendingQuoteRequests { get; set; }
    public IList<Invoice> NewSentInvoices { get; set; } = [];

    // ── Rooli ────────────────────────────────────────────────────────
    public bool IsAdmin { get; set; }
    public string WelcomeName { get; set; } = string.Empty;
}
