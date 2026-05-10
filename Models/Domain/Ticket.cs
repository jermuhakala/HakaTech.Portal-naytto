namespace HakaTech.Portal.Models.Domain;

/// <summary>Tiketin elinkaaren tilat: avoin → käsittelyssä → ratkaistu/suljettu.</summary>
public enum TicketStatus
{
    Open,            // Avoin — uusi tiketti, ei vielä otettu käsittelyyn
    InProgress,      // Käsittelyssä — admin työstää
    WaitingCustomer, // Odottaa asiakasta — pyydetty lisätietoja
    Resolved,        // Ratkaistu — odottaa asiakkaan vahvistusta
    Closed           // Suljettu — lopullisesti valmis
}

/// <summary>Tiketin kiireellisyys. Vaikuttaa SLA-vasteaikoihin.</summary>
public enum TicketPriority
{
    Low,      // Matala
    Normal,   // Normaali
    High,     // Korkea (kiireellinen)
    Critical  // Kriittinen (vaatii välittömän reagoinnin)
}

/// <summary>Tiketin aihealue. Helpottaa tikettien jakamista oikealle tiimille.</summary>
public enum TicketCategory
{
    Network,   // Verkko (palomuuri, VPN, langaton)
    Hardware,  // Laitteet (tulostimet, työasemat, palvelimet)
    Software,  // Ohjelmistot (sovellukset, päivitykset)
    Email,     // Sähköposti (M365, postilaatikot)
    Access,    // Käyttöoikeudet (salasanat, AD)
    Other      // Muut (kategorialuokituksen ulkopuolelle jäävät)
}

/// <summary>
/// Tukipyyntö (tiketti). Asiakkaan käyttäjä luo tiketin, admin ottaa sen
/// käsittelyyn ja kommunikointi tapahtuu kommenttien kautta.
/// </summary>
public class Ticket
{
    public int Id { get; set; }

    /// <summary>Tiketin lyhyt otsikko, näkyy listoissa.</summary>
    public string Title       { get; set; } = string.Empty;

    /// <summary>Pidempi vapaa kuvaus ongelmasta.</summary>
    public string Description { get; set; } = string.Empty;

    public TicketStatus   Status   { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public TicketCategory Category { get; set; } = TicketCategory.Other;

    /// <summary>Milloin tiketti luotiin (UTC).</summary>
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    /// <summary>Milloin tikettiä on viimeksi muokattu (UTC).</summary>
    public DateTime UpdatedAt  { get; set; } = DateTime.UtcNow;

    /// <summary>Milloin tiketti merkittiin ratkaistuksi. null jos vielä avoin.</summary>
    public DateTime? ResolvedAt{ get; set; }

    // ── Tiketin luoja (asiakkaan käyttäjä) ────────────────────────
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }

    // ── Admin-vastuuhenkilö ───────────────────────────────────────
    // Nullable — kunnes joku admin ottaa tiketin käsittelyyn.
    public string? AssignedToUserId { get; set; }
    public ApplicationUser? AssignedToUser { get; set; }

    // ── Mille asiakasyritykselle tiketti kuuluu ───────────────────
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Tiketin kommentit (keskustelu asiakkaan ja admin välillä).</summary>
    public ICollection<TicketComment>    Comments    { get; set; } = new List<TicketComment>();

    /// <summary>Tikettiin liitetyt tiedostot (kuvakaappaukset, lokit jne.).</summary>
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();

    /// <summary>Asiakkaan jättämä palaute, kun tiketti on suljettu (1–5 tähteä).</summary>
    public TicketFeedback? Feedback { get; set; }
}
