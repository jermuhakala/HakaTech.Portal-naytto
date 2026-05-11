using System.ComponentModel.DataAnnotations;
using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Uuden tiketin luontilomakkeen tiedonsiirtomalli (ViewModel).
/// Tämä luokka kuvaa tarkalleen ne kentät, joita lomakkeella on —
/// EI suoraan tietokantaa, vaan käyttöliittymän tarpeita varten.
///
/// Käyttö:
///   1) Controller luo tämän olion ja antaa sen näkymälle (GET Create)
///   2) Selain lähettää lomakkeen takaisin → ASP.NET täyttää tämän olion automaattisesti (POST Create)
///   3) Controller muuntaa tästä Ticket-entiteetin tietokantaan tallennettavaksi
///
/// DataAnnotations-attribuutit ([Required], [StringLength], [Display]) tekevät:
///   - Selainpuolen validoinnin (jQuery Validation)
///   - Palvelinpuolen validoinnin (ModelState.IsValid)
///   - Lomakkeen kenttien näyttönimet automaattisesti
/// </summary>
public class TicketCreateViewModel
{
    /// <summary>Tiketin lyhyt otsikko, näytetään listoissa.</summary>
    // [Required] = kenttä on pakollinen. Jos puuttuu, näytetään tämä virheviesti.
    [Required(ErrorMessage = "Otsikko on pakollinen.")]
    // [StringLength(300)] = enintään 300 merkkiä. Suojaa tietokantaa pitkiltä syötöiltä.
    [StringLength(300)]
    // [Display] = näkyvä nimi lomakkeen labelissa.
    [Display(Name = "Otsikko")]
    public string Title { get; set; } = string.Empty;  // = "" estää null-arvon, jolloin koodi on yksinkertaisempaa

    /// <summary>Pidempi kuvaus ongelmasta — näytetään tikettisivulla isona tekstilohkona.</summary>
    [Required(ErrorMessage = "Kuvaus on pakollinen.")]
    [Display(Name = "Kuvaus")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Tiketin aihealue (Network, Hardware, Software, Email, Access, Other).</summary>
    [Display(Name = "Kategoria")]
    // Oletusarvo "Other" varmistaa että kenttä ei ole määrittelemättömässä tilassa.
    public TicketCategory Category { get; set; } = TicketCategory.Other;

    /// <summary>Prioriteetti (Low, Normal, High, Critical). Vaikuttaa SLA-vasteaikoihin.</summary>
    [Display(Name = "Prioriteetti")]
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    /// <summary>
    /// Mille asiakasyritykselle tiketti kuuluu. Adminille pakollinen valinta,
    /// asiakaskäyttäjälle controller asettaa automaattisesti palvelinpuolella
    /// käyttäjän omasta yrityksestä (turvallisuussyy: ei voi luoda tikettiä
    /// toisen yrityksen nimissä lomakkeen kenttää manipuloimalla).
    /// </summary>
    [Display(Name = "Asiakas")]
    public int? CustomerId { get; set; }

    /// <summary>
    /// Asiakaspudotusvalikon vaihtoehdot adminin lomakkeella. Tätä EI lähetetä
    /// selaimen lomakkeesta takaisin — controller täyttää sen joka kerta uudestaan.
    /// </summary>
    public IEnumerable<SelectListItem> CustomerOptions { get; set; } = [];
}

/// <summary>
/// Admin-lomake statuksen ja vastuuhenkilön muuttamiseen.
/// </summary>
public class TicketEditViewModel
{
    public int Id { get; set; }

    [Display(Name = "Tila")]
    public TicketStatus Status { get; set; }

    [Display(Name = "Prioriteetti")]
    public TicketPriority Priority { get; set; }

    [Display(Name = "Vastuuhenkilö")]
    public string? AssignedToUserId { get; set; }
    public IEnumerable<SelectListItem> StaffOptions { get; set; } = [];
}

/// <summary>
/// Uuden kommentin lisääminen tikettiin.
/// </summary>
public class TicketCommentViewModel
{
    public int TicketId { get; set; }

    [Required(ErrorMessage = "Kommentti ei voi olla tyhjä.")]
    [Display(Name = "Kommentti")]
    public string Content { get; set; } = string.Empty;

    /// <summary>Vain admin voi merkitä sisäiseksi.</summary>
    [Display(Name = "Sisäinen muistiinpano (vain admin näkee)")]
    public bool IsInternal { get; set; } = false;
}
