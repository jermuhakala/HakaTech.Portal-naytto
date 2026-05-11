// Validointiattribuutit.
using System.ComponentModel.DataAnnotations;
// AnnouncementType-enum.
using HakaTech.Portal.Models.Domain;

// Nimiavaruus.
namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Tiedotteen luonti- ja muokkauslomake. Käytetään sekä uuden tiedotteen
/// luomiseen että olemassaolevan muokkaamiseen.
/// </summary>
// Dual-purpose ViewModel: sama luokka palvelee sekä Create- että Edit-toimintoja.
// Controlleri tunnistaa kumpi on kyseessä URL:n parametrista (id).
public class AnnouncementFormViewModel
{
    // Otsikko on pakollinen — tiedote täytyy aina nimetä.
    [Required(ErrorMessage = "Otsikko on pakollinen.")]
    // Enintään 300 merkkiä — pitkiä otsikoita rajoitetaan.
    [StringLength(300)]
    [Display(Name = "Otsikko")]
    public string Title { get; set; } = string.Empty;

    // Sisältö on pakollinen — tiedotteessa täytyy olla jotain luettavaa.
    [Required(ErrorMessage = "Sisältö on pakollinen.")]
    [Display(Name = "Sisältö")]
    // HTML-muotoinen sisältö on mahdollinen (WYSIWYG-editori).
    // XSS-suojaus hoidetaan renderoinnin yhteydessä Razor-näkymässä (Html.Raw + sanitointi).
    public string Content { get; set; } = string.Empty;

    // Tiedotteen tyyppi vaikuttaa näkymän väritykseen (Info=sininen, Maintenance=oranssi, Warning=punainen).
    // Oletusarvo Info = neutraali tiedote.
    [Display(Name = "Tyyppi")]
    public AnnouncementType Type { get; set; } = AnnouncementType.Info;

    // Aikaikkuna: tiedote voidaan ajastaa tietylle aikavälille.
    // [DataType(DataType.DateTime)] = selain näyttää päivämäärä+aika-valitsimen.
    [Display(Name = "Voimassa alkaen")]
    [DataType(DataType.DateTime)]
    // "DateTime?" = nullable. null = tiedote näkyy heti julkaisemisesta.
    public DateTime? ValidFrom { get; set; }

    [Display(Name = "Voimassa saakka")]
    [DataType(DataType.DateTime)]
    // null = tiedote ei vanhene automaattisesti — admin piilottaa manuaalisesti.
    public DateTime? ValidUntil { get; set; }

    // Julkaisukytkin — false = luonnos (vain admin näkee).
    // Oletuksena true = heti julkinen kun tallennetaan.
    [Display(Name = "Julkaistu")]
    public bool IsPublished { get; set; } = true;
}
