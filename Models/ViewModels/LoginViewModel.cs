using System.ComponentModel.DataAnnotations;

namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Kirjautumislomakkeen malli. Sisältää sähköpostin, salasanan ja
/// "muista minut" -valinnan. Validointiattribuutit varmistavat
/// että lomake on oikein täytetty ennen palvelimelle lähetystä.
/// </summary>
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

    /// <summary>Pidennetty istunto: eväste säilyy myös selaimen sulkemisen jälkeen.</summary>
    [Display(Name = "Muista minut")]
    public bool RememberMe { get; set; }

    /// <summary>Mihin osoitteeseen ohjataan onnistuneen kirjautumisen jälkeen.</summary>
    public string? ReturnUrl { get; set; }
}
