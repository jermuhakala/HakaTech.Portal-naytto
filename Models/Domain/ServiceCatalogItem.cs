namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Palvelukatalogin tuote — yksittäinen myytävä palvelu.
/// Asiakkaat selaavat näitä ja voivat pyytää tarjouksen (<see cref="QuoteRequest"/>).
/// </summary>
public class ServiceCatalogItem
{
    public int Id { get; set; }

    /// <summary>Palvelun nimi.</summary>
    public string  Name        { get; set; } = string.Empty;

    /// <summary>Pidempi kuvaus, mitä palvelu sisältää.</summary>
    public string  Description { get; set; } = string.Empty;

    /// <summary>Aihealue, esim. "Tietoturva", "Ylläpito", "Pilvipalvelut".</summary>
    public string? Category    { get; set; }

    /// <summary>Lähtöhinta euroissa. null = "pyydä tarjous".</summary>
    public decimal? Price      { get; set; }

    /// <summary>Onko palvelu listalla — false piilottaa sen asiakkailta.</summary>
    public bool    IsActive    { get; set; } = true;

    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    /// <summary>Tähän palveluun tehdyt tarjouspyynnöt.</summary>
    public ICollection<QuoteRequest> QuoteRequests { get; set; } = new List<QuoteRequest>();
}
