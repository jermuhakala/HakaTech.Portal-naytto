namespace HakaTech.Portal.Models.Domain;

/// <summary>Tarjouspyynnön elinkaari.</summary>
public enum QuoteRequestStatus
{
    Pending,    // Saapunut, odottaa adminin reagointia
    InProgress, // Käsittelyssä — admin valmistelee tarjousta
    Sent,       // Tarjous lähetetty asiakkaalle
    Accepted,   // Asiakas hyväksyi tarjouksen
    Declined    // Asiakas hylkäsi tai admin perui
}

/// <summary>
/// Asiakkaan tekemä tarjouspyyntö palvelukatalogin tuotteesta.
/// </summary>
public class QuoteRequest
{
    public int Id { get; set; }

    // ── Palvelu, josta tarjousta pyydetään ────────────────────────
    public int ServiceCatalogItemId { get; set; }
    public ServiceCatalogItem? Service { get; set; }

    // ── Asiakas ja pyynnön tehnyt käyttäjä ────────────────────────
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }

    /// <summary>Asiakkaan vapaa viesti tarjouspyynnön mukaan.</summary>
    public string? Message    { get; set; }

    /// <summary>Adminin sisäiset muistiinpanot — eivät näy asiakkaalle.</summary>
    public string? AdminNotes { get; set; }

    public QuoteRequestStatus Status    { get; set; } = QuoteRequestStatus.Pending;
    public DateTime           CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime           UpdatedAt { get; set; } = DateTime.UtcNow;
}
