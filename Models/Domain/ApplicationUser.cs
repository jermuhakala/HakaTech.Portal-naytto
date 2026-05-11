// Tuodaan ASP.NET Coren Identity-kirjasto käyttöön.
// IdentityUser-luokka on Microsoftin valmis pohja käyttäjähallinnalle:
// se sisältää mm. Id, Email, PasswordHash, LockoutEnd-kentät jo valmiina.
using Microsoft.AspNetCore.Identity;

// Nimiavaruus — luokka kuuluu domain-mallien ryhmään.
namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Sovelluksen käyttäjä. Periytyy ASP.NET Coren <see cref="IdentityUser"/>-luokasta,
/// joka tarjoaa valmiit ominaisuudet kuten käyttäjätunnus, salasanan tiiviste,
/// sähköpostiosoite ja lukitustiedot. Tähän luokkaan lisätään HakaTechin
/// omat lisäkentät (esim. koko nimi ja yrityskytkentä).
/// </summary>
// "public class ApplicationUser : IdentityUser" = perintä (inheritance).
// ": IdentityUser" tarkoittaa: ApplicationUser-luokka SAA KAIKKI IdentityUser-luokan
// ominaisuudet (Id, UserName, Email, PasswordHash, EmailConfirmed, jne.)
// LISÄKSI siihen lisätään HakaTechin omat kentät alla.
//
// Tietokannassa tämä näkyy yhtenä AspNetUsers-tauluna, jossa on
// sekä Identityn sarakkeet että alla määritellyt lisäkentät.
public class ApplicationUser : IdentityUser
{
    /// <summary>Käyttäjän koko nimi (etu- ja sukunimi).</summary>
    // Tämä on HakaTechin oma lisäkenttä — IdentityUser-luokassa ei ole FullName-kenttää.
    // Näytetään kommenteissa, lähetetyissä sähköposteissa ja tervehdysteksteissä.
    // "= string.Empty" = oletusarvo tyhjä merkkijono (ei null).
    public string FullName { get; set; } = string.Empty;

    // ── Yrityskytkentä ────────────────────────────────────────────────────────
    // Käyttäjä voi kuulua yhteen asiakasyritykseen.
    // HakaTechin omat työntekijät (admin-rooli) eivät kuulu mihinkään yritykseen
    // → heidän CustomerId-arvonsa on null.

    /// <summary>Asiakasyrityksen tunnus, johon käyttäjä kuuluu (null = HakaTechin oma henkilöstö).</summary>
    // "int?" = nullable integer. Kysymysmerkki sallii null-arvon.
    // null = ei yrityskytkentää = admin-käyttäjä.
    // Jos arvo on annettu (esim. 42) → käyttäjä kuuluu kyseiseen asiakasyritykseen.
    public int? CustomerId { get; set; }

    /// <summary>Navigaatio kytkettyyn asiakasyritykseen. EF Core lataa tämän tarvittaessa.</summary>
    // "Customer?" = nullable navigaatio. EF Core täyttää tämän automaattisesti
    // kun kyselyyn lisätään .Include(u => u.Customer).
    // Ilman Includia tämä on null vaikka CustomerId olisi asetettu.
    public Customer? Customer { get; set; }

    /// <summary>True = asiakasyrityksen pääkäyttäjä, joka voi hallita yrityksensä muita käyttäjiä.</summary>
    // Normaali asiakaskäyttäjä ei voi luoda/muokata muita käyttäjiä.
    // Jos IsCustomerAdmin = true, käyttäjä pääsee CustomerUser-hallintasivulle.
    // bool = totuusarvo; oletuksena false (ei pääkäyttäjä).
    public bool IsCustomerAdmin { get; set; }

    /// <summary>
    /// Käyttäjän mukautettu koontinäyttöjärjestys: pilkuilla eroteltu lista
    /// widget-avaimista, jotka näytetään dashboardilla ja missä järjestyksessä.
    /// </summary>
    // Esimerkki arvosta: "tickets,invoices,kpi,calendar,quickactions"
    // Tallennetaan yhtenä merkkijonona tietokantaan (ei erillistä taulua).
    // null = käyttäjä ei ole mukauttanut → käytetään oletusarvoa.
    public string? DashboardLayout { get; set; }
}
