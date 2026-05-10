using System.ComponentModel.DataAnnotations;

namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Asiakasyrityksen uuden käyttäjän luontilomake. Käytetään kun yrityksen
/// pääkäyttäjä (customer-admin) lisää uuden käyttäjän omaan yritykseensä.
/// </summary>
public class CustomerUserFormViewModel
{
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

    [Required(ErrorMessage = "Salasana on pakollinen.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Salasanan on oltava vähintään 8 merkkiä.")]
    [DataType(DataType.Password)]
    [Display(Name = "Salasana")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Salasanat eivät täsmää.")]
    [Display(Name = "Salasana uudelleen")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Yrityksen pääkäyttäjä")]
    public bool IsCustomerAdmin { get; set; }
}

/// <summary>Asiakasyrityksen olemassaolevan käyttäjän muokkauslomake (ei salasanaa).</summary>
public class CustomerUserEditViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

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

    [Display(Name = "Yrityksen pääkäyttäjä")]
    public bool IsCustomerAdmin { get; set; }
}
