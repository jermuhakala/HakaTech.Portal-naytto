namespace HakaTech.Portal.Models.Domain;

/// <summary>Tiedotteen tyyppi — ohjaa myös käyttöliittymän väritystä.</summary>
public enum AnnouncementType
{
    Info,         // Yleinen tiedote (sininen)
    Maintenance,  // Suunniteltu huoltokatko (oranssi)
    Warning       // Varoitus / akuutti häiriö (punainen)
}

/// <summary>
/// Admin-ylläpitäjän tekemä tiedote, joka näkyy käyttäjille portaalin etusivulla.
/// Tiedotteella voi olla aikaikkuna (ValidFrom...ValidUntil), jonka aikana se on aktiivinen.
/// </summary>
public class Announcement
{
    public int Id { get; set; }

    /// <summary>Tiedotteen otsikko.</summary>
    public string           Title      { get; set; } = string.Empty;

    /// <summary>Tiedotteen tekstisisältö (voi sisältää HTML:ää, sanitoidaan näytettäessä).</summary>
    public string           Content    { get; set; } = string.Empty;

    public AnnouncementType Type       { get; set; } = AnnouncementType.Info;

    /// <summary>Mistä alkaen tiedote näkyy. null = heti.</summary>
    public DateTime? ValidFrom  { get; set; }

    /// <summary>Mihin asti tiedote näkyy. null = ei vanhene.</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>Onko tiedote julkaistu. False = vain admin näkee (luonnos).</summary>
    public bool      IsPublished { get; set; } = true;

    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
