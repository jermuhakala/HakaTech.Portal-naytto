namespace HakaTech.Portal.Models.Domain;

public class TicketComment
{
    public int Id { get; set; }

    public string Content   { get; set; } = string.Empty;
    public bool   IsInternal{ get; set; } = false; // true = vain admin näkee

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int    TicketId { get; set; }
    public Ticket? Ticket  { get; set; }

    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }
}
