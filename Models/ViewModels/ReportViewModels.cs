namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Raporttisivun pääsivun malli. Sisältää viikoittaiset tikettiluvut,
/// kuukausittaiset laskutusluvut sekä yhteenvetoja.
/// </summary>
public class ReportIndexViewModel
{
    public int WeeksBack  { get; set; } = 12;
    public int MonthsBack { get; set; } = 12;

    public List<WeeklyTicketStat>   WeeklyStats    { get; set; } = [];
    public List<MonthlyBillingStat> MonthlyStats   { get; set; } = [];

    // Tila-jakauma kaikista tiketeistä
    public int CountOpen            { get; set; }
    public int CountInProgress      { get; set; }
    public int CountWaitingCustomer { get; set; }
    public int CountResolved        { get; set; }
    public int CountClosed          { get; set; }

    // Laskutuksen kokonaissummat valitulta ajanjaksolta
    public decimal TotalBilledExcl  { get; set; }
    public decimal TotalBilledIncl  { get; set; }
    public decimal TotalPaid        { get; set; }

    // Tikettipalaute
    public double?  AvgFeedbackRating { get; set; }
    public int      FeedbackCount     { get; set; }
}

/// <summary>Viikkotason tikettitilasto (luotuja vs. ratkaistuja).</summary>
public class WeeklyTicketStat
{
    /// <summary>Viikon otsikko, esim. "Vk 15 / 2026".</summary>
    public string Label    { get; set; } = string.Empty;

    /// <summary>Viikon aikana luotujen tikettien määrä.</summary>
    public int    Created  { get; set; }

    /// <summary>Viikon aikana ratkaistujen tikettien määrä.</summary>
    public int    Resolved { get; set; }
}

/// <summary>Kuukausitason laskutustilasto.</summary>
public class MonthlyBillingStat
{
    /// <summary>Kuukauden otsikko, esim. "huhtikuu 2026".</summary>
    public string  Label        { get; set; } = string.Empty;
    public int     InvoiceCount { get; set; }
    public decimal TotalExcl    { get; set; }
    public decimal TotalVat     { get; set; }
    public decimal TotalIncl    { get; set; }
    public decimal PaidAmount   { get; set; }
}
