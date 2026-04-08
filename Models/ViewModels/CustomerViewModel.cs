using System.ComponentModel.DataAnnotations;

namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// ViewModel asiakkaan luontia ja muokkausta varten.
/// </summary>
public class CustomerFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Yritysnimi on pakollinen.")]
    [StringLength(200)]
    [Display(Name = "Yritysnimi")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Y-tunnus on pakollinen.")]
    [StringLength(20)]
    [RegularExpression(@"^\d{7}-\d$", ErrorMessage = "Y-tunnus muodossa 1234567-8")]
    [Display(Name = "Y-tunnus")]
    public string BusinessId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yhteyssähköposti on pakollinen.")]
    [EmailAddress(ErrorMessage = "Virheellinen sähköpostiosoite.")]
    [StringLength(200)]
    [Display(Name = "Yhteyssähköposti")]
    public string ContactEmail { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Virheellinen puhelinnumero.")]
    [StringLength(50)]
    [Display(Name = "Puhelinnumero")]
    public string? Phone { get; set; }

    [StringLength(300)]
    [Display(Name = "Osoite")]
    public string? Address { get; set; }

    [Display(Name = "Aktiivinen")]
    public bool IsActive { get; set; } = true;
}
