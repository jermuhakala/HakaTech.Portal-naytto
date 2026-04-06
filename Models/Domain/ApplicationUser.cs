using Microsoft.AspNetCore.Identity;

namespace HakaTech.Portal.Models.Domain;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    // Navigaatio: asiakas voi olla linkitettynä yhteen yritykseen
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
}
