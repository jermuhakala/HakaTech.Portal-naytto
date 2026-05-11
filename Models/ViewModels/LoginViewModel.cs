// System.ComponentModel.DataAnnotations sisältää validointiattribuutit kuten
// [Required], [EmailAddress], [DataType] — nämä kertovat sekä selaimelle
// että palvelimelle miten kentät validoidaan.
using System.ComponentModel.DataAnnotations;

// Nimiavaruus — ViewModels on erillinen kansio UI-lomakkeiden malleille.
namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Kirjautumislomakkeen malli. Sisältää sähköpostin, salasanan ja
/// "muista minut" -valinnan. Validointiattribuutit varmistavat
/// että lomake on oikein täytetty ennen palvelimelle lähetystä.
/// </summary>
// ViewModel-malli toimii "lomakkeen edustajana":
//  1) Controller luo tämän olion ja antaa sen Login.cshtml-näkymälle (GET)
//  2) Käyttäjä täyttää lomakkeen ja lähettää sen (POST)
//  3) ASP.NET täyttää tämän olion automaattisesti lomakkeen kentistä
//  4) Controller tarkistaa ModelState.IsValid ja käsittelee kirjautumisen
public class LoginViewModel
{
    // [Required] = kenttä ei voi olla tyhjä. Jos on tyhjä, näytetään virheviesti.
    [Required(ErrorMessage = "Sähköposti on pakollinen.")]
    // [EmailAddress] = validoi että arvo on sähköpostiosoitteen muotoinen (x@y.z).
    // Huom: tämä ei tarkista onko osoite oikea, vain että muoto on oikein.
    [EmailAddress(ErrorMessage = "Virheellinen sähköpostiosoite.")]
    // [Display] = näkyvä nimi lomakkeen labelissa (ei muuta tietokantaa).
    [Display(Name = "Sähköposti")]
    // Oletusarvo string.Empty estää null-vertailut koodissa.
    public string Email { get; set; } = string.Empty;

    // [Required] = salasana pakollinen.
    [Required(ErrorMessage = "Salasana on pakollinen.")]
    // [DataType(DataType.Password)] = ohje selaimelle näyttää tämä kenttä salasanakentällä
    // (kirjaimet korvataan pisteillä). EI validoi mitään, vain UI-vihje.
    [DataType(DataType.Password)]
    [Display(Name = "Salasana")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Pidennetty istunto: eväste säilyy myös selaimen sulkemisen jälkeen.</summary>
    // bool = totuusarvo, oletuksena false = ei muisteta.
    // Jos true → SignInManager luo pysyvän evästeen (persistent cookie)
    // jonka voimassaoloaika on pidempi kuin istuntoevästeen.
    [Display(Name = "Muista minut")]
    public bool RememberMe { get; set; }

    /// <summary>Mihin osoitteeseen ohjataan onnistuneen kirjautumisen jälkeen.</summary>
    // ReturnUrl tallentaa sen sivun osoitteen, josta käyttäjä tuli kirjautumissivulle.
    // Esimerkki: käyttäjä yrittää avata /Ticket/Details/5 → siirretään kirjautumissivulle
    // → onnistuneen kirjautumisen jälkeen palataan takaisin /Ticket/Details/5.
    // TÄRKEYS: Controller tarkistaa aina Url.IsLocalUrl(ReturnUrl) ennen ohjaamista
    // → estää "open redirect" -hyökkäyksen (ulkopuoliselle sivulle ohjaaminen).
    public string? ReturnUrl { get; set; }
}
