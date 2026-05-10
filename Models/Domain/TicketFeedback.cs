namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Asiakkaan jättämä palaute kun tiketti on suljettu. Yksi tiketti
/// voi saada vain yhden palautteen (uniikki indeksi DB:ssä).
/// </summary>
public class TicketFeedback
{
    public int Id { get; set; }

    // ── Tiketti, jota palaute koskee ──────────────────────────────
    public int     TicketId { get; set; }
    public Ticket? Ticket   { get; set; }

    // ── Palautteen jättäjä ────────────────────────────────────────
    public string           UserId { get; set; } = string.Empty;
    public ApplicationUser? User   { get; set; }

    /// <summary>Arvosana asteikolla 1–5 (1 = huono, 5 = erinomainen).</summary>
    public int     Rating      { get; set; }

    /// <summary>Vapaamuotoinen sanallinen palaute (vapaaehtoinen).</summary>
    public string? Comment     { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
