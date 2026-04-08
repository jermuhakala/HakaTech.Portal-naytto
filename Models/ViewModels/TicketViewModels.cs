using System.ComponentModel.DataAnnotations;
using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HakaTech.Portal.Models.ViewModels;

/// <summary>
/// Uuden tiketin luontilomake (asiakas tai admin).
/// </summary>
public class TicketCreateViewModel
{
    [Required(ErrorMessage = "Otsikko on pakollinen.")]
    [StringLength(300)]
    [Display(Name = "Otsikko")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kuvaus on pakollinen.")]
    [Display(Name = "Kuvaus")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Kategoria")]
    public TicketCategory Category { get; set; } = TicketCategory.Other;

    [Display(Name = "Prioriteetti")]
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    // Admin voi valita asiakkaan manuaalisesti; asiakas saa sen automaattisesti
    [Display(Name = "Asiakas")]
    public int? CustomerId { get; set; }
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
