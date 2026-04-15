namespace HakaTech.Portal.Models.Domain;

public enum TicketStatus
{
    Open,           // Avoin
    InProgress,     // Käsittelyssä
    WaitingCustomer,// Odottaa asiakasta
    Resolved,       // Ratkaistu
    Closed          // Suljettu
}

public enum TicketPriority
{
    Low,    // Matala
    Normal, // Normaali
    High,   // Korkea
    Critical// Kriittinen
}

public enum TicketCategory
{
    Network,    // Verkko
    Hardware,   // Laitteet
    Software,   // Ohjelmistot
    Email,      // Sähköposti
    Access,     // Käyttöoikeudet
    Other       // Muut
}

public class Ticket
{
    public int Id { get; set; }

    public string Title       { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public TicketStatus   Status   { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public TicketCategory Category { get; set; } = TicketCategory.Other;

    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt{ get; set; }

    // Kuka asiakkaan puolelta loi tiketin
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }

    // Admin-vastuuhenkilö (nullable – ei vielä osoitettu)
    public string? AssignedToUserId { get; set; }
    public ApplicationUser? AssignedToUser { get; set; }

    // Mille asiakkaalle tiketti kuuluu
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    // Navigaatio kommentteihin ja liitteisiin
    public ICollection<TicketComment>    Comments    { get; set; } = new List<TicketComment>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();

    public TicketFeedback? Feedback { get; set; }
}
