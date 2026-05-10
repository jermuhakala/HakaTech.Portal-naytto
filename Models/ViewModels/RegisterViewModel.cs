using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Käyttäjän rekisteröintilomake (Admin-puolella käytettävä). Tällä
/// luodaan uusi käyttäjä ja kytketään se haluttuun rooliin ja asiakkaaseen.
/// </summary>
public class RegisterViewModel
{
    [Required(ErrorMessage = "Koko nimi on pakollinen.")]
    [StringLength(100)]
    [Display(Name = "Koko nimi")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sähköposti on pakollinen.")]
    [EmailAddress(ErrorMessage = "Virheellinen sähköpostiosoite.")]
    [Display(Name = "Sähköposti")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Salasana on pakollinen.")]
    [StringLength(100, MinimumLength = 8,
        ErrorMessage = "Salasanan on oltava vähintään 8 merkkiä pitkä.")]
    [DataType(DataType.Password)]
    [Display(Name = "Salasana")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Salasanan vahvistus — täytyy täsmätä Password-kentän kanssa.</summary>
    [Required(ErrorMessage = "Salasanan vahvistus on pakollinen.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Salasanat eivät täsmää.")]
    [Display(Name = "Vahvista salasana")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Käyttäjän rooli: "Admin" tai "Customer".</summary>
    [Display(Name = "Rooli")]
    public string Role { get; set; } = "Customer";

    /// <summary>Mihin asiakasyritykseen käyttäjä kuuluu (vain Customer-roolille).</summary>
    [Display(Name = "Asiakasyritys")]
    public int? CustomerId { get; set; }

    /// <summary>Asiakasyritysten lista pudotusvalikkoa varten.</summary>
    public IEnumerable<SelectListItem> CustomerOptions { get; set; } = [];
}
