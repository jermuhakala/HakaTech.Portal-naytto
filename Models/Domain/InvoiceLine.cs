namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Yksittäinen laskurivi: tuote/palvelu, määrä ja yksikköhinta.
/// Rivin summa on Quantity × UnitPrice.
/// </summary>
public class InvoiceLine
{
    public int Id { get; set; }

    /// <summary>Rivin selite asiakkaalle (esim. "IT-tuki kuukausimaksu").</summary>
    public string  Description { get; set; } = string.Empty;

    /// <summary>Kappalemäärä tai tunnit. Salli kymmenykset (esim. 3,5 h).</summary>
    public decimal Quantity    { get; set; } = 1;

    /// <summary>Verollinen yksikköhinta (€).</summary>
    public decimal UnitPrice   { get; set; }

    public int     InvoiceId { get; set; }
    public Invoice? Invoice  { get; set; }
}
