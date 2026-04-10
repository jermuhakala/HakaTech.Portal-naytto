namespace HakaTech.Portal.Models.Domain;

public enum AnnouncementType
{
    Info,        // Tiedote
    Maintenance, // Huoltokatko
    Warning      // Varoitus
}

public class Announcement
{
    public int Id { get; set; }

    public string           Title      { get; set; } = string.Empty;
    public string           Content    { get; set; } = string.Empty;
    public AnnouncementType Type       { get; set; } = AnnouncementType.Info;

    public DateTime? ValidFrom  { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool      IsPublished { get; set; } = true;

    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
