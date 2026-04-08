using System.ComponentModel.DataAnnotations;

namespace HakaTech.Portal.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Sähköposti on pakollinen.")]
    [EmailAddress(ErrorMessage = "Virheellinen sähköpostiosoite.")]
    [Display(Name = "Sähköposti")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Salasana on pakollinen.")]
    [DataType(DataType.Password)]
    [Display(Name = "Salasana")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Muista minut")]
    public bool RememberMe { get; set; }

    // Palautusosoite onnistuneen kirjautumisen jälkeen
    public string? ReturnUrl { get; set; }
}
