// Validointiattribuutit.
using System.ComponentModel.DataAnnotations;

// Nimiavaruus.
namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// ViewModel asiakkaan luontia ja muokkausta varten.
/// Käytetään sekä uuden asiakasyrityksen lisäämiseen (Create)
/// että olemassaolevan muokkaamiseen (Edit).
/// </summary>
// CustomerFormViewModel on ns. "dual-purpose" ViewModel:
// sama lomake toimii sekä luontiin (Id=0) että muokkaukseen (Id>0).
// Controlleri osaa tunnistaa kummasta on kyse Id-arvon perusteella.
public class CustomerFormViewModel
{
    // Id = 0 tarkoittaa uutta asiakasta (ei vielä tietokannassa).
    // Id > 0 tarkoittaa olemassaolevan muokkaamista.
    // Tätä EI validoida pakolliseksi — uudella asiakkaalla ei ole vielä Id:tä.
    public int Id { get; set; }

    // [Required] = pakollinen kenttä. Yritysnimi on perustieto.
    [Required(ErrorMessage = "Yritysnimi on pakollinen.")]
    // [StringLength(200)] = maksimipituus 200 merkkiä. Suojaa tietokannan sarakkeen pituusrajoitukselta.
    [StringLength(200)]
    [Display(Name = "Yritysnimi")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Y-tunnus on pakollinen.")]
    [StringLength(20)]
    // [RegularExpression] = kenttä täytyy vastata annettua kaavaa (regular expression).
    // Kaava: "^\d{7}-\d$"
    //   ^ = alkaa tästä
    //   \d{7} = tasan seitsemän numeroa
    //   - = väliviiva
    //   \d = yksi numero (tarkistusnumero)
    //   $ = päättyy tähän
    // Esimerkki: "1234567-8" täsmää, "12345-6" ei.
    [RegularExpression(@"^\d{7}-\d$", ErrorMessage = "Y-tunnus muodossa 1234567-8")]
    [Display(Name = "Y-tunnus")]
    public string BusinessId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yhteyssähköposti on pakollinen.")]
    // [EmailAddress] = tarkistaa sähköpostimuodon.
    [EmailAddress(ErrorMessage = "Virheellinen sähköpostiosoite.")]
    [StringLength(200)]
    [Display(Name = "Yhteyssähköposti")]
    public string ContactEmail { get; set; } = string.Empty;

    // [Phone] = tarkistaa puhelinnumeron muodon (löyhästi — hyväksyy monet kansainväliset muodot).
    [Phone(ErrorMessage = "Virheellinen puhelinnumero.")]
    [StringLength(50)]
    [Display(Name = "Puhelinnumero")]
    // "string?" = nullable — puhelinnumero ei ole pakollinen.
    public string? Phone { get; set; }

    [StringLength(300)]
    [Display(Name = "Osoite")]
    // Nullable — osoite ei ole pakollinen.
    public string? Address { get; set; }

    [Display(Name = "Aktiivinen")]
    // Valintaruutu (checkbox). Oletuksena true = uusi asiakas on heti aktiivinen.
    public bool IsActive { get; set; } = true;
}
