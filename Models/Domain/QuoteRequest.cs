namespace HakaTech.Portal.Models.Domain;

public enum QuoteRequestStatus
{
    Pending,    // Odottaa käsittelyä
    InProgress, // Käsittelyssä
    Sent,       // Tarjous lähetetty
    Accepted,   // Hyväksytty
    Declined    // Hylätty
}

public class QuoteRequest
{
    public int Id { get; set; }

    public int ServiceCatalogItemId { get; set; }
    public ServiceCatalogItem? Service { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }

    public string? Message    { get; set; }
    public string? AdminNotes { get; set; }

    public QuoteRequestStatus Status    { get; set; } = QuoteRequestStatus.Pending;
    public DateTime           CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime           UpdatedAt { get; set; } = DateTime.UtcNow;
}
