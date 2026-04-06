namespace HakaTech.Portal.Models.Domain;

public class InvoiceLine
{
    public int Id { get; set; }

    public string  Description { get; set; } = string.Empty;
    public decimal Quantity    { get; set; } = 1;
    public decimal UnitPrice   { get; set; }

    public int     InvoiceId { get; set; }
    public Invoice? Invoice  { get; set; }
}
