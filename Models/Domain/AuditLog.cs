namespace HakaTech.Portal.Models.Domain;

public class AuditLog
{
    public int      Id          { get; set; }
    public DateTime Timestamp   { get; set; } = DateTime.UtcNow;

    public string? UserId    { get; set; }
    public string? UserEmail { get; set; }   // denormalisoitu historiaa varten

    /// <summary>Esim. Login, TicketCreated, TicketStatusChanged, InvoiceDownloaded, UserCreated.</summary>
    public string  Action     { get; set; } = string.Empty;

    /// <summary>Esim. Ticket, Invoice, User.</summary>
    public string? EntityType { get; set; }
    public string? EntityId   { get; set; }

    /// <summary>Vapaamuotoinen lisätieto.</summary>
    public string? Details    { get; set; }
    public string? IpAddress  { get; set; }
}
