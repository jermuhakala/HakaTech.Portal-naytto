namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Tietopankin artikkeli. Kuuluu johonkin kategoriaan ja sisältää
/// HTML-muotoisen sisällön. Asiakkaat lukevat näitä self-service-ohjeina.
/// </summary>
public class KnowledgeBaseArticle
{
    public int    Id         { get; set; }
    public string Title      { get; set; } = string.Empty;

    /// <summary>HTML-muotoinen sisältö. Sanitoidaan ennen tallennusta XSS-suojan vuoksi.</summary>
    public string Content    { get; set; } = string.Empty;

    // ── Kategoria, johon artikkeli kuuluu ─────────────────────────
    public int                  CategoryId { get; set; }
    public KnowledgeBaseCategory? Category { get; set; }

    /// <summary>Onko artikkeli julkaistu. False = vain admin näkee (luonnos).</summary>
    public bool IsPublished { get; set; } = true;

    /// <summary>Korostetaanko artikkeli etusivulla / kategorian alussa.</summary>
    public bool IsFeatured  { get; set; }

    /// <summary>Lukukerrat — kasvaa joka näytöllä, auttaa tunnistamaan suosituimmat artikkelit.</summary>
    public int  ViewCount   { get; set; }

    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt  { get; set; } = DateTime.UtcNow;

    public string           CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser   { get; set; }
}
