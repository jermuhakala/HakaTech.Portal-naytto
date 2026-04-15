namespace HakaTech.Portal.Models.ViewModels;

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

public class WeeklyTicketStat
{
    public string Label    { get; set; } = string.Empty; // "Vk 15 / 2026"
    public int    Created  { get; set; }
    public int    Resolved { get; set; }
}

public class MonthlyBillingStat
{
    public string  Label        { get; set; } = string.Empty; // "huhtikuu 2026"
    public int     InvoiceCount { get; set; }
    public decimal TotalExcl    { get; set; }
    public decimal TotalVat     { get; set; }
    public decimal TotalIncl    { get; set; }
    public decimal PaidAmount   { get; set; }
}
