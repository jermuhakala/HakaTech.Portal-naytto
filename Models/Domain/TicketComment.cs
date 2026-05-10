namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Kommentti yksittäiseen tikettiin. Kommentointi muodostaa
/// keskustelun asiakkaan ja admin-tukihenkilön välille.
/// </summary>
public class TicketComment
{
    public int Id { get; set; }

    /// <summary>Kommentin tekstisisältö.</summary>
    public string Content   { get; set; } = string.Empty;

    /// <summary>
    /// True = sisäinen muistiinpano, jonka vain admin näkee.
    /// Asiakkaalle nämä rivit eivät renderöidy.
    /// </summary>
    public bool   IsInternal{ get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Tiketti, johon kommentti kuuluu ───────────────────────────
    public int    TicketId { get; set; }
    public Ticket? Ticket  { get; set; }

    // ── Kommentin kirjoittaja ─────────────────────────────────────
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }
}
