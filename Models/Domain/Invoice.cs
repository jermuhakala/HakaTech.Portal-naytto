namespace HakaTech.Portal.Models.Domain;

public enum InvoiceStatus
{
    Draft,   // Luonnos
    Sent,    // Lähetetty
    Unpaid,  // Maksamatta
    Paid,    // Maksettu
    Overdue  // Erääntynyt
}

public class Invoice
{
    public int Id { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty; // esim. INV-2025-08

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate     { get; set; }
    public DateTime? PaidAt     { get; set; }

    public string? Notes { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    // Laskurivit
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();

    // Lasketut kentät (ei tallennu kantaan)
    public decimal SubTotal    => Lines.Sum(l => l.Quantity * l.UnitPrice);
    public decimal VatAmount   => SubTotal * VatRate;
    public decimal TotalAmount => SubTotal + VatAmount;
    public decimal VatRate     { get; set; } = 0.255m; // 25.5 % ALV
}
