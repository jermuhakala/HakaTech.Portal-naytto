// Valmis pudotusvalikon rakennustyökalu (SelectListItem).
using Microsoft.AspNetCore.Mvc.Rendering;
// Validointiattribuutit.
using System.ComponentModel.DataAnnotations;

// Nimiavaruus.
namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Käyttäjän rekisteröintilomake (Admin-puolella käytettävä). Tällä
/// luodaan uusi käyttäjä ja kytketään se haluttuun rooliin ja asiakkaaseen.
/// </summary>
// Huom: tätä lomaketta käyttää VAIN admin. Tavallinen käyttäjä ei voi
// rekisteröityä itse — käyttäjätilit luodaan vain admin-toimesta.
// Tämä on tietoinen tietoturvaratkaisu: hallittu pääsynhallinta.
public class RegisterViewModel
{
    // [Required] = kenttä pakollinen ennen lomakkeen lähettämistä.
    [Required(ErrorMessage = "Koko nimi on pakollinen.")]
    // [StringLength(100)] = enintään 100 merkkiä. Estää pitkien syötteiden tallennuksen.
    [StringLength(100)]
    [Display(Name = "Koko nimi")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sähköposti on pakollinen.")]
    // [EmailAddress] = tarkistaa sähköpostimuodon (x@y.z).
    // Sähköposti toimii myös kirjautumistunnuksena (UserName = Email).
    [EmailAddress(ErrorMessage = "Virheellinen sähköpostiosoite.")]
    [Display(Name = "Sähköposti")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Salasana on pakollinen.")]
    // [StringLength(100, MinimumLength = 8)] = enintään 100 merkkiä, vähintään 8 merkkiä.
    // Minimipituus 8 merkkiä on perusvaatimus tietoturvalliselle salasanalle.
    // ASP.NET Identity vaatii myös muita ehtoja (isot/pienet kirjaimet, numerot jne.)
    // — nämä konfiguroidaan Program.cs:ssä PasswordOptions-asetuksissa.
    [StringLength(100, MinimumLength = 8,
        ErrorMessage = "Salasanan on oltava vähintään 8 merkkiä pitkä.")]
    // [DataType(DataType.Password)] = selain näyttää kentän salasanakenttänä.
    [DataType(DataType.Password)]
    [Display(Name = "Salasana")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Salasanan vahvistus — täytyy täsmätä Password-kentän kanssa.</summary>
    [Required(ErrorMessage = "Salasanan vahvistus on pakollinen.")]
    [DataType(DataType.Password)]
    // [Compare("Password")] = tarkistaa että tämä kenttä on täsmälleen sama kuin Password-kenttä.
    // Estää kirjoitusvirheet salasanassa: jos molemmat eivät täsmää, virheviesti näytetään.
    [Compare("Password", ErrorMessage = "Salasanat eivät täsmää.")]
    [Display(Name = "Vahvista salasana")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Käyttäjän rooli: "Admin" tai "Customer".</summary>
    // Rooli määrää mitä toimintoja käyttäjä voi tehdä:
    //   Admin = kaikki toiminnot, näkee kaikki tiketit/laskut
    //   Customer = vain oman yrityksen tiketit ja laskut
    // Oletusarvo "Customer" — admin-roolia ei jaeta vahingossa.
    [Display(Name = "Rooli")]
    public string Role { get; set; } = "Customer";

    /// <summary>Mihin asiakasyritykseen käyttäjä kuuluu (vain Customer-roolille).</summary>
    // "int?" = nullable. null = admin-käyttäjä, ei yrityskytkentää.
    // Jos rooli on "Customer", tämä on pakollinen (tarkistetaan controllerissa).
    [Display(Name = "Asiakasyritys")]
    public int? CustomerId { get; set; }

    /// <summary>Asiakasyritysten lista pudotusvalikkoa varten.</summary>
    // IEnumerable<SelectListItem> = lista, josta ASP.NET rakentaa <select>-elementin automaattisesti.
    // Jokainen SelectListItem sisältää näkyvän tekstin (CompanyName) ja arvon (Id).
    // Tätä listaa EI lähetetä lomakkeesta takaisin — controller täyttää sen aina uudestaan
    // kun lomake näytetään uudelleen (validointivirhe tai GET-pyyntö).
    public IEnumerable<SelectListItem> CustomerOptions { get; set; } = [];
}
