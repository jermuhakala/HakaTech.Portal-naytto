using System.ComponentModel.DataAnnotations;
using HakaTech.Portal.Models.Domain;

namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Tiedotteen luonti- ja muokkauslomake. Käytetään sekä uuden tiedotteen
/// luomiseen että olemassaolevan muokkaamiseen.
/// </summary>
public class AnnouncementFormViewModel
{
    [Required(ErrorMessage = "Otsikko on pakollinen.")]
    [StringLength(300)]
    [Display(Name = "Otsikko")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sisältö on pakollinen.")]
    [Display(Name = "Sisältö")]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "Tyyppi")]
    public AnnouncementType Type { get; set; } = AnnouncementType.Info;

    [Display(Name = "Voimassa alkaen")]
    [DataType(DataType.DateTime)]
    public DateTime? ValidFrom { get; set; }

    [Display(Name = "Voimassa saakka")]
    [DataType(DataType.DateTime)]
    public DateTime? ValidUntil { get; set; }

    [Display(Name = "Julkaistu")]
    public bool IsPublished { get; set; } = true;
}
