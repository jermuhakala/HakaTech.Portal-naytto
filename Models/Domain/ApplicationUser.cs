using Microsoft.AspNetCore.Identity;

namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Sovelluksen käyttäjä. Periytyy ASP.NET Coren <see cref="IdentityUser"/>-luokasta,
/// joka tarjoaa valmiit ominaisuudet kuten käyttäjätunnus, salasanan tiiviste,
/// sähköpostiosoite ja lukitustiedot. Tähän luokkaan lisätään HakaTechin
/// omat lisäkentät (esim. koko nimi ja yrityskytkentä).
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Käyttäjän koko nimi (etu- ja sukunimi).</summary>
    public string FullName { get; set; } = string.Empty;

    // ── Yrityskytkentä ────────────────────────────────────────────
    // Käyttäjä voi kuulua yhteen asiakasyritykseen. HakaTechin omat
    // työntekijät (admin) eivät kuulu mihinkään yritykseen → CustomerId = null.

    /// <summary>Asiakasyrityksen tunnus, johon käyttäjä kuuluu (null = HakaTechin oma henkilöstö).</summary>
    public int? CustomerId { get; set; }

    /// <summary>Navigaatio kytkettyyn asiakasyritykseen. EF Core lataa tämän tarvittaessa.</summary>
    public Customer? Customer { get; set; }

    /// <summary>True = asiakasyrityksen pääkäyttäjä, joka voi hallita yrityksensä muita käyttäjiä.</summary>
    public bool IsCustomerAdmin { get; set; }

    /// <summary>
    /// Käyttäjän mukautettu koontinäyttöjärjestys: pilkuilla eroteltu lista
    /// widget-avaimista, jotka näytetään dashboardilla ja missä järjestyksessä.
    /// </summary>
    public string? DashboardLayout { get; set; }
}
