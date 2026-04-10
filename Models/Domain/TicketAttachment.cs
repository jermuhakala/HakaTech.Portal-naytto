namespace HakaTech.Portal.Models.Domain;

public class TicketAttachment
{
    public int Id { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public string UploadedByUserId { get; set; } = string.Empty;
    public ApplicationUser? UploadedByUser { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }
}
