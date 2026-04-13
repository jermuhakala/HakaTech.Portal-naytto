using System.ComponentModel.DataAnnotations;
using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HakaTech.Portal.Models.ViewModels;

/// <summary>Admin-lomake: luo tai muokkaa etäyhteyttä.</summary>
public class RemoteDesktopConnectionFormViewModel
{
    public int Id { get; set; }  // 0 = uusi

    [Required(ErrorMessage = "Nimi on pakollinen.")]
    [StringLength(200, ErrorMessage = "Nimi voi olla enintään 200 merkkiä.")]
    [Display(Name = "Yhteyden nimi")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Protokolla on valittava.")]
    [Display(Name = "Protokolla")]
    public RemoteDesktopProtocol Protocol { get; set; } = RemoteDesktopProtocol.Rdp;

    [Required(ErrorMessage = "Isäntänimi tai IP-osoite on pakollinen.")]
    [StringLength(500, ErrorMessage = "Isäntänimi voi olla enintään 500 merkkiä.")]
    [Display(Name = "Isäntänimi / IP-osoite")]
    public string Hostname { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "Portin on oltava välillä 1–65535.")]
    [Display(Name = "Portti")]
    public int Port { get; set; } = 3389;

    [StringLength(200, ErrorMessage = "Käyttäjätunnus voi olla enintään 200 merkkiä.")]
    [Display(Name = "Käyttäjätunnus")]
    public string? Username { get; set; }

    [StringLength(500, ErrorMessage = "Salasana voi olla enintään 500 merkkiä.")]
    [Display(Name = "Salasana")]
    [DataType(DataType.Password)]
    public string? PlainPassword { get; set; }

    [Display(Name = "Ohita varmennevirheet (RDP)")]
    public bool IgnoreCert { get; set; } = true;

    [StringLength(50)]
    [Display(Name = "Suojaustaso (RDP)")]
    public string Security { get; set; } = "any";

    [StringLength(2000, ErrorMessage = "Muistiinpanot voivat olla enintään 2000 merkkiä.")]
    [Display(Name = "Muistiinpanot")]
    public string? Notes { get; set; }

    [Display(Name = "Aktiivinen")]
    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "Asiakas on valittava.")]
    [Display(Name = "Asiakas")]
    public int CustomerId { get; set; }

    public IEnumerable<SelectListItem> CustomerOptions { get; set; } = [];
}

/// <summary>Asiakasnäkymä: yhden yhteyden kortti.</summary>
public class RemoteDesktopConnectionCardViewModel
{
    public int    Id       { get; set; }
    public string Name     { get; set; } = string.Empty;
    public RemoteDesktopProtocol Protocol { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public int    Port     { get; set; }
    public string? Notes   { get; set; }
}
