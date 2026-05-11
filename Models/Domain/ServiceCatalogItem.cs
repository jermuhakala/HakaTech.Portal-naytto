// Nimiavaruus.
namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Palvelukatalogin tuote — yksittäinen myytävä palvelu.
/// Asiakkaat selaavat näitä ja voivat pyytää tarjouksen (<see cref="QuoteRequest"/>).
/// </summary>
// Palvelukatalogi toimii kuin verkkokaupan tuotekatalogi:
// Admin lisää palveluita (esim. "M365 Business Premium -lisenssit"),
// asiakas selaa listaa ja voi pyytää tarjouksen haluamastaan palvelusta.
public class ServiceCatalogItem
{
    // Pääavain.
    public int Id { get; set; }

    /// <summary>Palvelun nimi.</summary>
    // Lyhyt ja kuvaava nimi. Esim. "Microsoft 365 Business Premium".
    public string  Name        { get; set; } = string.Empty;

    /// <summary>Pidempi kuvaus, mitä palvelu sisältää.</summary>
    // Kaikki yksityiskohdat palvelusta. Näytetään palvelun detaljisivulla.
    public string  Description { get; set; } = string.Empty;

    /// <summary>Aihealue, esim. "Tietoturva", "Ylläpito", "Pilvipalvelut".</summary>
    // Vapaa kategoriateksti — ei enum kuten tiketeissä. Adminin itse päättämä.
    // Nullable — ei pakollinen. Oletuksena null = kategoriaton.
    public string? Category    { get; set; }

    /// <summary>Lähtöhinta euroissa. null = "pyydä tarjous".</summary>
    // "decimal?" = nullable. null = hinta sovitaan tapauskohtaisesti.
    // Jos arvo on asetettu, se näytetään "alkaen X €/kk" -muodossa.
    // "decimal" siksi että raha vaatii tarkkaa laskentaa (ei pyöristysvirheitä).
    public decimal? Price      { get; set; }

    /// <summary>Onko palvelu listalla — false piilottaa sen asiakkailta.</summary>
    // Soft delete -periaate: palvelua ei poisteta, vain piilotetaan.
    public bool    IsActive    { get; set; } = true;

    // Milloin palvelu lisättiin katalogiin.
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    /// <summary>Tähän palveluun tehdyt tarjouspyynnöt.</summary>
    // Yksi ServiceCatalogItem → monta QuoteRequest-oliota.
    // Tyhjä lista oletuksena.
    public ICollection<QuoteRequest> QuoteRequests { get; set; } = new List<QuoteRequest>();
}
