// Validointiattribuutit.
using System.ComponentModel.DataAnnotations;
// RemoteDesktopProtocol-enum (Rdp, Vnc, Ssh).
using HakaTech.Portal.Models.Domain;
// SelectListItem pudotusvalikkoa varten.
using Microsoft.AspNetCore.Mvc.Rendering;

// Nimiavaruus.
namespace HakaTech.Portal.Models.ViewModels;

/// <summary>Admin-lomake: luo tai muokkaa etäyhteyttä.</summary>
// Tällä lomakkeella admin tallentaa asiakkaan etäyhteysasetukset.
// Salasana käsitellään erityisellä huolella: PlainPassword ei koskaan mene tietokantaan.
public class RemoteDesktopConnectionFormViewModel
{
    // 0 = uusi yhteys, >0 = olemassaoleva muokattava yhteys.
    public int Id { get; set; }  // 0 = uusi

    // Yhteyden näyttönimi käyttäjälle. Pakollinen, enintään 200 merkkiä.
    [Required(ErrorMessage = "Nimi on pakollinen.")]
    [StringLength(200, ErrorMessage = "Nimi voi olla enintään 200 merkkiä.")]
    [Display(Name = "Yhteyden nimi")]
    public string Name { get; set; } = string.Empty;

    // Protokolla: RDP, VNC tai SSH.
    [Required(ErrorMessage = "Protokolla on valittava.")]
    [Display(Name = "Protokolla")]
    public RemoteDesktopProtocol Protocol { get; set; } = RemoteDesktopProtocol.Rdp;

    // Kohdekoneen osoite tai nimi.
    [Required(ErrorMessage = "Isäntänimi tai IP-osoite on pakollinen.")]
    [StringLength(500, ErrorMessage = "Isäntänimi voi olla enintään 500 merkkiä.")]
    [Display(Name = "Isäntänimi / IP-osoite")]
    public string Hostname { get; set; } = string.Empty;

    // TCP-portti 1–65535. Oletusarvo 3389 = RDP:n oletusportti.
    [Range(1, 65535, ErrorMessage = "Portin on oltava välillä 1–65535.")]
    [Display(Name = "Portti")]
    public int Port { get; set; } = 3389;

    // Kirjautumistunnus kohteen koneelle. Vapaaehtoinen.
    [StringLength(200, ErrorMessage = "Käyttäjätunnus voi olla enintään 200 merkkiä.")]
    [Display(Name = "Käyttäjätunnus")]
    public string? Username { get; set; }

    // SALASANAKÄSITTELY LOMAKKEELLA:
    // PlainPassword = käyttäjän syöttämä selkokielinen salasana.
    // Tätä ei koskaan tallenneta tietokantaan sellaisenaan.
    // Controller ottaa tämän arvon, salaa sen Data Protection -API:lla
    // ja tallentaa salatun version RemoteDesktopConnection.EncryptedPassword-kenttään.
    [StringLength(500, ErrorMessage = "Salasana voi olla enintään 500 merkkiä.")]
    [Display(Name = "Salasana")]
    // [DataType(DataType.Password)] = selain piilottaa syötetyn tekstin.
    [DataType(DataType.Password)]
    public string? PlainPassword { get; set; }

    // Hyväksytäänkö itseallekirjoitettu sertifikaatti. Yleensä true sisäverkoissa.
    [Display(Name = "Ohita varmennevirheet (RDP)")]
    public bool IgnoreCert { get; set; } = true;

    // RDP-suojaustaso. "any" = automaattinen neuvottelu.
    [StringLength(50)]
    [Display(Name = "Suojaustaso (RDP)")]
    public string Security { get; set; } = "any";

    // Guacamolen oma yhteyden ID — täytetään kun Guacamole-integraatio on käytössä.
    [StringLength(100, ErrorMessage = "Guacamole-yhteyden ID voi olla enintään 100 merkkiä.")]
    [Display(Name = "Guacamole-yhteyden ID")]
    public string? GuacamoleConnectionId { get; set; }

    // Vapaa muistiinpano yhteydestä (vain admin näkee).
    [StringLength(2000, ErrorMessage = "Muistiinpanot voivat olla enintään 2000 merkkiä.")]
    [Display(Name = "Muistiinpanot")]
    public string? Notes { get; set; }

    // Onko yhteys käytössä. False = piilotettu listalta.
    [Display(Name = "Aktiivinen")]
    public bool IsActive { get; set; } = true;

    // Mille asiakkaalle yhteys kuuluu — pakollinen.
    [Required(ErrorMessage = "Asiakas on valittava.")]
    [Display(Name = "Asiakas")]
    public int CustomerId { get; set; }

    // Asiakaspudotusvalikko. Täytetään controllerissa.
    public IEnumerable<SelectListItem> CustomerOptions { get; set; } = [];
}

/// <summary>Asiakasnäkymä: yhden yhteyden kortti.</summary>
// Tätä kevyttä ViewModel-luokkaa käytetään asiakasnäkymässä (RemoteDesktop/Index).
// Se ei sisällä arkaluonteisia tietoja (salasanaa, Guacamole-ID:tä) —
// vain ne tiedot joita tarvitaan kortin näyttämiseen käyttöliittymässä.
// Tiedon minimointiperiaate (GDPR / tietoturva): ei anneta enemmän tietoa kuin tarvitaan.
public class RemoteDesktopConnectionCardViewModel
{
    public int    Id       { get; set; }
    // Yhteyden näyttönimi.
    public string Name     { get; set; } = string.Empty;
    // Protokolla — näytetään ikonina tai tekstinä.
    public RemoteDesktopProtocol Protocol { get; set; }
    // Kohdekoneen osoite — näytetään kortin tiedoissa.
    public string Hostname { get; set; } = string.Empty;
    // Portti — näytetään kortin tiedoissa.
    public int    Port     { get; set; }
    // Muistiinpano — näytetään kortin alaosassa (vapaaehtoinen).
    public string? Notes   { get; set; }
}
