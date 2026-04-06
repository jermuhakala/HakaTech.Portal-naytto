namespace HakaTech.Portal.Models.Domain;

public class Customer
{
    public int Id { get; set; }

    public string CompanyName { get; set; } = string.Empty;   // Yritysnimi
    public string BusinessId   { get; set; } = string.Empty;  // Y-tunnus
    public string ContactEmail { get; set; } = string.Empty;
    public string? Phone       { get; set; }
    public string? Address     { get; set; }

    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public bool IsActive       { get; set; } = true;

    // Navigaatio
    public ICollection<ApplicationUser> Users     { get; set; } = new List<ApplicationUser>();
    public ICollection<Ticket>          Tickets   { get; set; } = new List<Ticket>();
    public ICollection<Invoice>         Invoices  { get; set; } = new List<Invoice>();
    public ICollection<Contract>        Contracts { get; set; } = new List<Contract>();
}
