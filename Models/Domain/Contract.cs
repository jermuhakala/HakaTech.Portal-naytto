namespace HakaTech.Portal.Models.Domain;

public enum ContractType
{
    Support24_7,    // Tuki 24/7 Pro
    SupportBusiness,// Tuki arkisin
    Managed,        // Managed IT
    OneTime         // Kertapalvelu
}

public class Contract
{
    public int Id { get; set; }

    public ContractType Type        { get; set; }
    public string       Description { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime EndDate   { get; set; }

    public int  TicketQuota    { get; set; } = 30; // tikettikiintiö/kk
    public int  TicketsUsed    { get; set; } = 0;

    public decimal MonthlyPrice { get; set; }
    public bool    IsActive     { get; set; } = true;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
}
