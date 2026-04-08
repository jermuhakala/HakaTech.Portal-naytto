using System.ComponentModel.DataAnnotations;

namespace HakaTech.Portal.Models.ViewModels;

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Nykyinen salasana on pakollinen.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nykyinen salasana")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Uusi salasana on pakollinen.")]
    [StringLength(100, MinimumLength = 8,
        ErrorMessage = "Salasanan on oltava vähintään 8 merkkiä pitkä.")]
    [DataType(DataType.Password)]
    [Display(Name = "Uusi salasana")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Salasanan vahvistus on pakollinen.")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Salasanat eivät täsmää.")]
    [Display(Name = "Vahvista uusi salasana")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
