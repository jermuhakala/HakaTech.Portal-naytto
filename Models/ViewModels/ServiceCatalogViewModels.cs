// Validointiattribuutit.
using System.ComponentModel.DataAnnotations;
// QuoteRequestStatus-enum.
using HakaTech.Portal.Models.Domain;

// Nimiavaruus.
namespace HakaTech.Portal.Models.ViewModels;

/// <summary>Adminin lomake palvelukatalogin tuotteen luomiseen ja muokkaamiseen.</summary>
// Tällä lomakkeella admin lisää tai muokkaa palveluita, joita asiakkaat voivat tilata.
public class ServiceCatalogItemFormViewModel
{
    // Palvelun nimi on pakollinen. Enintään 200 merkkiä.
    [Required(ErrorMessage = "Nimi on pakollinen.")]
    [StringLength(200)]
    [Display(Name = "Palvelun nimi")]
    public string Name { get; set; } = string.Empty;

    // Kuvaus on pakollinen — asiakkaan täytyy ymmärtää mitä palvelu sisältää.
    [Required(ErrorMessage = "Kuvaus on pakollinen.")]
    [Display(Name = "Kuvaus")]
    public string Description { get; set; } = string.Empty;

    // Kategoria on vapaaehtoinen teksti, ei enum.
    // Admin voi kirjoittaa "Tietoturva", "Pilvipalvelut" jne.
    [StringLength(100)]
    [Display(Name = "Kategoria")]
    public string? Category { get; set; }

    // Hinta on vapaaehtoinen — null = "pyydä tarjous".
    // [Range(0, 9999999)] = hinta täytyy olla positiivinen.
    [Display(Name = "Lähtöhinta (€)")]
    [Range(0, 9999999)]
    public decimal? Price { get; set; }

    // Näkyvyyskytkin — false piilottaa asiakkailta.
    [Display(Name = "Aktiivinen")]
    public bool IsActive { get; set; } = true;
}

/// <summary>Asiakkaan tarjouspyyntölomake palvelusta.</summary>
// Asiakas täyttää tämän lomakkeen haluaessaan tarjouksen.
// Lomake on tarkoituksella minimaalinen: asiakas valitsee palvelun ja kirjoittaa viestin.
public class QuoteRequestFormViewModel
{
    // [Required] = palvelun ID täytyy olla mukana. Tulee piilotettuna lomakkeen kentästä.
    [Required]
    public int ServiceCatalogItemId { get; set; }

    // Vapaaehtoinen lisäviesti adminille.
    // Esim. "Tarvitsemme ratkaisun 50 käyttäjälle. Haluamme myös koulutuksen."
    [StringLength(2000)]
    [Display(Name = "Viesti / lisätietoja")]
    public string? Message { get; set; }
}

/// <summary>Adminin lomake tarjouspyynnön tilan ja muistiinpanojen päivittämiseen.</summary>
// Admin näkee tarjouspyynnön ja voi muuttaa sen tilaa sekä lisätä sisäisiä muistiinpanoja.
// Sisäiset muistiinpanot eivät näy asiakkaalle.
public class QuoteRequestUpdateViewModel
{
    // Tarjouspyynnön tunniste — tarvitaan jotta tiedetään mitä päivitetään.
    public int Id { get; set; }

    // Uusi tila — admin valitsee: InProgress, Sent, Accepted, Declined.
    [Display(Name = "Tila")]
    public QuoteRequestStatus Status { get; set; }

    // Adminin sisäiset muistiinpanot — esim. hintalaskelmat tai neuvotteluhistoria.
    // Asiakkaalle ei näytetä.
    [StringLength(2000)]
    [Display(Name = "Muistiinpanot (sisäiset)")]
    public string? AdminNotes { get; set; }
}
