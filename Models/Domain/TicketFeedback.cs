namespace HakaTech.Portal.Models.Domain;

public class TicketFeedback
{
    public int Id { get; set; }

    public int     TicketId { get; set; }
    public Ticket? Ticket   { get; set; }

    public string           UserId { get; set; } = string.Empty;
    public ApplicationUser? User   { get; set; }

    /// <summary>Arvosana 1–5.</summary>
    public int     Rating      { get; set; }
    public string? Comment     { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
