// Nimiavaruus.
namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Tietopankin kategoria, johon artikkelit ryhmitellään.
/// Esim. "Yleistä", "Laskutus", "Tekniset ohjeet", "Tietoturva".
/// </summary>
// Kategoriat toimivat kuten kirjaston osastot: ne ryhmittelevät artikkelit aihepiireittäin.
// Asiakas voi selata tietopankkia kategorioittain tai hakea avainsanalla.
public class KnowledgeBaseCategory
{
    // Pääavain.
    public int    Id          { get; set; }
    // Kategorian nimi — näkyy navigaatiossa ja artikkelin yläosassa.
    public string Name        { get; set; } = string.Empty;
    // Lyhyt kuvaus mitä kategoria sisältää. Vapaaehtoinen.
    public string? Description { get; set; }

    /// <summary>Järjestysnumero — pienempi = ensin listalla.</summary>
    // Adminin tapa kontrolloida kategorioiden järjestystä.
    // Esim. SortOrder=1 = ensimmäisenä, SortOrder=99 = viimeisenä.
    // Oletusarvo on 0 (int:n oletusarvo C#:ssa).
    public int    SortOrder   { get; set; }

    /// <summary>Onko kategoria näkyvissä.</summary>
    // False = kategoria piilotettu asiakkailta (mutta admin näkee).
    // IsActive=false ei poista kategorian artikkeleita — ne vain piilotetaan.
    public bool   IsActive    { get; set; } = true;

    /// <summary>Tähän kategoriaan kuuluvat artikkelit.</summary>
    // "List<>" eikä "ICollection<>" koska tarvitaan luettua listaa suoraan
    // (ei pelkästään iterointia). "[]" = tyhjä lista oletuksena.
    public List<KnowledgeBaseArticle> Articles { get; set; } = [];
}
