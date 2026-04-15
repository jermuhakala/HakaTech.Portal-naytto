using Microsoft.AspNetCore.Identity;

namespace HakaTech.Portal.Models.Domain;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    // Navigaatio: asiakas voi olla linkitettynä yhteen yritykseen
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>True = asiakasyrityksen pääkäyttäjä, voi hallita yrityksen muita käyttäjiä.</summary>
    public bool IsCustomerAdmin { get; set; }

    /// <summary>Käyttäjän mukautettu koontinäyttöjärjestys: pilkuilla eroteltu widget-avain lista.</summary>
    public string? DashboardLayout { get; set; }
}
