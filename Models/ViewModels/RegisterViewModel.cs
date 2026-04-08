using System.ComponentModel.DataAnnotations;

namespace HakaTech.Portal.Models.ViewModels;

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

    [Required(ErrorMessage = "Salasanan vahvistus on pakollinen.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Salasanat eivät täsmää.")]
    [Display(Name = "Vahvista salasana")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
