namespace HakaTech.Portal.Models.Domain;

public class ServiceCatalogItem
{
    public int Id { get; set; }

    public string  Name        { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;
    public string? Category    { get; set; }   // Esim. "Tietoturva", "Ylläpito"
    public decimal? Price      { get; set; }   // Lähtöhinta (valinnainen)
    public bool    IsActive    { get; set; } = true;
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    public ICollection<QuoteRequest> QuoteRequests { get; set; } = new List<QuoteRequest>();
}
