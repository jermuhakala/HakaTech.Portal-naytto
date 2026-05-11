// Validointiattribuutit.
using System.ComponentModel.DataAnnotations;

// Nimiavaruus.
namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Asiakasyrityksen uuden käyttäjän luontilomake. Käytetään kun yrityksen
/// pääkäyttäjä (customer-admin) lisää uuden käyttäjän omaan yritykseensä.
/// </summary>
// Tämä lomake on eri kuin AccountController/Register:
// - Register: admin luo käyttäjiä eri yrityksiin (voi valita yrityksen)
// - CustomerUserFormViewModel: yrityksen oma pääkäyttäjä lisää uuden henkilön VAIN OMAAN yritykseensä
// Tietoturvaero: CustomerId tulee session käyttäjäprofiilista, ei lomakkeesta.
public class CustomerUserFormViewModel
{
    // Asiakasyrityksen ID — tulee piilotetusta kentästä lomakkeessa.
    // Controller varmistaa palvelinpuolella että tämä on kirjautuneen käyttäjän oma yritys
    // (ei voi luoda käyttäjiä toiseen yritykseen lomakkeen kenttiä manipuloimalla).
    public int CustomerId { get; set; }

    [Required(ErrorMessage = "Nimi on pakollinen.")]
    [StringLength(100)]
    [Display(Name = "Koko nimi")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sähköpostiosoite on pakollinen.")]
    [EmailAddress(ErrorMessage = "Virheellinen sähköpostiosoite.")]
    // [StringLength(256)] = ASP.NET Identityn sähköpostin maksimipituus on 256 merkkiä.
    [StringLength(256)]
    [Display(Name = "Sähköposti")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Salasana on pakollinen.")]
    // Vähintään 8 merkkiä — perusvaatimus tietoturvalle.
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Salasanan on oltava vähintään 8 merkkiä.")]
    [DataType(DataType.Password)]
    [Display(Name = "Salasana")]
    public string Password { get; set; } = string.Empty;

    // [Required] = vahvistus on pakollinen.
    [Required]
    [DataType(DataType.Password)]
    // nameof(Password) = "Password" — käytetään nameof:ia jotta kääntäjä huomaa jos kenttänimi muuttuu.
    [Compare(nameof(Password), ErrorMessage = "Salasanat eivät täsmää.")]
    [Display(Name = "Salasana uudelleen")]
    public string ConfirmPassword { get; set; } = string.Empty;

    // Voidaanko tämä käyttäjä asettaa yrityksen pääkäyttäjäksi.
    // Pääkäyttäjä voi hallita yrityksen muita käyttäjiä.
    [Display(Name = "Yrityksen pääkäyttäjä")]
    public bool IsCustomerAdmin { get; set; }
}

/// <summary>Asiakasyrityksen olemassaolevan käyttäjän muokkauslomake (ei salasanaa).</summary>
// Muokkauslomakkeessa salasanaa ei muokata — siihen on oma lomakkeensa (ChangePassword).
// Tässä muokataan vain nimeä, sähköpostia ja pääkäyttäjyyttä.
public class CustomerUserEditViewModel
{
    // Käyttäjän string-muotoinen GUID-tunniste (ASP.NET Identity).
    // Pakollinen — tarvitaan jotta tiedetään kenen tietoja muokataan.
    [Required]
    public string UserId { get; set; } = string.Empty;

    // Asiakasyrityksen ID — varmistaa että muokataan oikean yrityksen käyttäjää.
    public int CustomerId { get; set; }

    [Required(ErrorMessage = "Nimi on pakollinen.")]
    [StringLength(100)]
    [Display(Name = "Koko nimi")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sähköpostiosoite on pakollinen.")]
    [EmailAddress(ErrorMessage = "Virheellinen sähköpostiosoite.")]
    [StringLength(256)]
    [Display(Name = "Sähköposti")]
    public string Email { get; set; } = string.Empty;

    // Pääkäyttäjyys voidaan antaa tai poistaa muokkauksessa.
    [Display(Name = "Yrityksen pääkäyttäjä")]
    public bool IsCustomerAdmin { get; set; }
}
