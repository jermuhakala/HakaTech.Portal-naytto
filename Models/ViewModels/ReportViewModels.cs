// Nimiavaruus.
namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Raporttisivun pääsivun malli. Sisältää viikoittaiset tikettiluvut,
/// kuukausittaiset laskutusluvut sekä yhteenvetoja.
/// </summary>
// Kaikki raporttidatan koostaa ReportController.Index()-metodi
// ja antaa sen Index.cshtml-näkymälle renderöitäväksi.
// Raportit näkyvät vain adminille ([Authorize(Roles = "Admin")]).
public class ReportIndexViewModel
{
    // Kuinka monta viikkoa taaksepäin tilastot lasketaan.
    // Oletusarvo 12 = viimeiset 12 viikkoa. Min 4, max 52 (rajoitetaan controllerissa).
    public int WeeksBack  { get; set; } = 12;

    // Kuinka monta kuukautta taaksepäin laskutustilastot lasketaan.
    // Oletusarvo 12 = viimeinen vuosi. Min 3, max 24.
    public int MonthsBack { get; set; } = 12;

    // Lista viikkotason tikettitilastoista. Järjestyksessä vanhin ensin.
    public List<WeeklyTicketStat>   WeeklyStats    { get; set; } = [];
    // Lista kuukausitason laskutustilastoista. Järjestyksessä vanhin ensin.
    public List<MonthlyBillingStat> MonthlyStats   { get; set; } = [];

    // Tila-jakauma kaikista tiketeistä (kaikki aikarajat)
    // Nämä täytetään tila-jakauman count-kyselystä (GroupBy Status).
    public int CountOpen            { get; set; }  // Avoimet.
    public int CountInProgress      { get; set; }  // Käsittelyssä.
    public int CountWaitingCustomer { get; set; }  // Odottaa asiakasta.
    public int CountResolved        { get; set; }  // Ratkaistut.
    public int CountClosed          { get; set; }  // Suljetut.

    // Laskutuksen kokonaissummat valitulta ajanjaksolta.
    // "decimal" = tarkka laskenta rahalle.
    public decimal TotalBilledExcl  { get; set; }  // Yhteensä ilman ALV:tä.
    public decimal TotalBilledIncl  { get; set; }  // Yhteensä sisältäen ALV:n.
    public decimal TotalPaid        { get; set; }  // Maksettu yhteensä.

    // Tikettipalautteiden yhteenveto.
    // "double?" = nullable double. null = ei yhtään palautetta (ei voi laskea keskiarvoa).
    public double?  AvgFeedbackRating { get; set; }  // Tähtiarvosanojen keskiarvo (1.0–5.0).
    public int      FeedbackCount     { get; set; }   // Kuinka monta palautetta on annettu.
}

/// <summary>Viikkotason tikettitilasto (luotuja vs. ratkaistuja).</summary>
// Yksi tämä olio vastaa yhtä viikkoa raportissa.
public class WeeklyTicketStat
{
    /// <summary>Viikon otsikko, esim. "Vk 15 / 2026".</summary>
    // Muotoiltu X-akselin labeliksi viivakaaviolle.
    public string Label    { get; set; } = string.Empty;

    /// <summary>Viikon aikana luotujen tikettien määrä.</summary>
    public int    Created  { get; set; }

    /// <summary>Viikon aikana ratkaistujen tikettien määrä.</summary>
    // Jos Created > Resolved → backlog kasvaa. Jos Resolved > Created → backlog vähenee.
    public int    Resolved { get; set; }
}

/// <summary>Kuukausitason laskutustilasto.</summary>
// Yksi tämä olio vastaa yhtä kuukautta raportissa.
public class MonthlyBillingStat
{
    /// <summary>Kuukauden otsikko, esim. "huhtikuu 2026".</summary>
    // Muotoiltu X-akselin labeliksi pylväskaaviolle.
    public string  Label        { get; set; } = string.Empty;
    // Kuinka monta laskua kyseisellä kuukaudella luotiin.
    public int     InvoiceCount { get; set; }
    // Kuukauden laskutus ilman ALV:tä.
    public decimal TotalExcl    { get; set; }
    // Kuukauden ALV-summa yhteensä.
    public decimal TotalVat     { get; set; }
    // Kuukauden laskutus ALV:n kanssa.
    public decimal TotalIncl    { get; set; }
    // Kuukaudella maksettu yhteensä (vain Paid-tilaiset laskut).
    public decimal PaidAmount   { get; set; }
}
