namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Tietopankin kategoria, johon artikkelit ryhmitellään.
/// Esim. "Yleistä", "Laskutus", "Tekniset ohjeet", "Tietoturva".
/// </summary>
public class KnowledgeBaseCategory
{
    public int    Id          { get; set; }
    public string Name        { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Järjestysnumero — pienempi = ensin listalla.</summary>
    public int    SortOrder   { get; set; }

    /// <summary>Onko kategoria näkyvissä.</summary>
    public bool   IsActive    { get; set; } = true;

    /// <summary>Tähän kategoriaan kuuluvat artikkelit.</summary>
    public List<KnowledgeBaseArticle> Articles { get; set; } = [];
}
