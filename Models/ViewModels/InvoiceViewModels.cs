// Validointiattribuutit lomakkeen kenttien tarkistamiseen.
using System.ComponentModel.DataAnnotations;
// Domain-mallit — InvoiceStatus-enum ja muut tyypit.
using HakaTech.Portal.Models.Domain;
// SelectListItem pudotusvalikkoja varten.
using Microsoft.AspNetCore.Mvc.Rendering;

// Nimiavaruus.
namespace HakaTech.Portal.Models.ViewModels;

/// <summary>Yksittäinen laskurivi laskun luontilomakkeessa.</summary>
// InvoiceLineInputModel vastaa yhden laskurivin tietoja lomakkeella.
// Nimi "InputModel" viittaa siihen, että tämä on nimenomaan syöttödataa varten
// (ei domain-entiteetti, ei näyttödataa varten).
public class InvoiceLineInputModel
{
    // Kuvaus on pakollinen — laskurivi täytyy selittää asiakkaalle.
    [Required(ErrorMessage = "Kuvaus on pakollinen.")]
    // Enintään 300 merkkiä.
    [StringLength(300)]
    [Display(Name = "Kuvaus")]
    public string Description { get; set; } = string.Empty;

    // [Required] = pakollinen.
    [Required]
    // [Range(min, max)] = arvo täytyy olla tällä välillä.
    // 0.01 tarkoittaa ettei voi olla nolla (täytyy olla edes 0,01 kappaletta tai tuntia).
    [Range(0.01, 9999, ErrorMessage = "Määrä on oltava positiivinen.")]
    [Display(Name = "Määrä")]
    // Oletusarvo 1 = yksi kappale tai tunti.
    public decimal Quantity { get; set; } = 1;

    [Required]
    // Hinta voi olla 0 (ilmainen rivi, esim. bonus tai hyvitys).
    [Range(0, 999999, ErrorMessage = "Hinta on oltava positiivinen.")]
    [Display(Name = "À-hinta (€)")]
    // "À-hinta" = yksikköhinta (ranskankielisestä "à" = yksikköä kohti).
    public decimal UnitPrice { get; set; }
}

/// <summary>Laskun luontilomakkeen malli — sisältää laskun tiedot ja vähintään yhden rivin.</summary>
// Tämä ViewModel yhdistää laskun ylätason tiedot (asiakas, numero, päivämäärät)
// ja dynaamisen määrän rivejä (InvoiceLineInputModel-lista).
// Lomakkeessa admin voi lisätä/poistaa rivejä JavaScriptillä.
public class InvoiceCreateViewModel
{
    // Pakollinen — lasku täytyy kuulua jollekin asiakkaalle.
    [Required(ErrorMessage = "Asiakas on valittava.")]
    [Display(Name = "Asiakas")]
    public int CustomerId { get; set; }
    // Pudotusvalikon vaihtoehdot. Täytetään controllerissa, ei lähetetä lomakkeessa takaisin.
    public IEnumerable<SelectListItem> CustomerOptions { get; set; } = [];

    // Laskunumero täytyy olla uniikki. Pakollinen.
    [Required(ErrorMessage = "Laskunumero on pakollinen.")]
    [StringLength(50)]
    [Display(Name = "Laskunumero")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required]
    // [DataType(DataType.Date)] = selain näyttää päivämäärävalitsimen (date picker).
    [DataType(DataType.Date)]
    [Display(Name = "Laskun päivämäärä")]
    // Oletusarvo = tänään. Uusi lasku on oletuksena päivätty tähän päivään.
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Eräpäivä")]
    // Oletusarvo = 14 päivää eteenpäin. Suomessa yleinen laskun maksuaika on 14 päivää.
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(14);

    // ALV-prosentti desimaalina. 0–1 välillä (0 = 0%, 1 = 100%).
    // Oletusarvo 0.255 = Suomen yleinen ALV 25,5 %.
    // "m"-pääte = decimal-vakio (ei float).
    [Range(0, 1, ErrorMessage = "ALV-prosentti 0–100 %.")]
    [Display(Name = "ALV-%")]
    public decimal VatRate { get; set; } = 0.255m;

    [StringLength(1000)]
    [Display(Name = "Muistiinpanot")]
    // Nullable — muistiinpanot eivät ole pakollisia.
    public string? Notes { get; set; }

    // Laskurivit – vähintään yksi vaaditaan (tarkistetaan controllerissa erikseen).
    // "List<>" eikä "IEnumerable<>" koska lomake rakentaa listan dynaamisesti JavaScriptillä.
    // Oletusarvo: lista jossa on yksi tyhjä rivi (jotta lomakkeessa näkyy aina ainakin yksi rivi).
    public List<InvoiceLineInputModel> Lines { get; set; } =
        [new InvoiceLineInputModel()];
}

/// <summary>Pieni lomake laskun tilan muuttamiseen (esim. Sent → Paid).</summary>
// Tätä ei käytetä laskun luomiseen vaan vain tilan päivittämiseen.
// Pieni, tarkasti rajattu ViewModel — ei anna muuttaa mitään muuta kuin tilan.
public class InvoiceStatusViewModel
{
    // Laskun tunniste — tarvitaan jotta tiedetään minkä laskun tilaa muutetaan.
    public int Id { get; set; }

    // Uusi tila — admin valitsee pudotusvalikosta.
    [Display(Name = "Tila")]
    public InvoiceStatus Status { get; set; }

    // Maksupäivä näytetään jos tila vaihdetaan Paid-tilaan.
    // Nullable — ei ole pakollinen jos tila ei ole Paid.
    [DataType(DataType.Date)]
    [Display(Name = "Maksupäivä")]
    public DateTime? PaidAt { get; set; }
}
