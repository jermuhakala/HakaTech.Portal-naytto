using System.ComponentModel.DataAnnotations;
using HakaTech.Portal.Models.Domain;

namespace HakaTech.Portal.Models.ViewModels;

public class ServiceCatalogItemFormViewModel
{
    [Required(ErrorMessage = "Nimi on pakollinen.")]
    [StringLength(200)]
    [Display(Name = "Palvelun nimi")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kuvaus on pakollinen.")]
    [Display(Name = "Kuvaus")]
    public string Description { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Kategoria")]
    public string? Category { get; set; }

    [Display(Name = "Lähtöhinta (€)")]
    [Range(0, 9999999)]
    public decimal? Price { get; set; }

    [Display(Name = "Aktiivinen")]
    public bool IsActive { get; set; } = true;
}

public class QuoteRequestFormViewModel
{
    [Required]
    public int ServiceCatalogItemId { get; set; }

    [StringLength(2000)]
    [Display(Name = "Viesti / lisätietoja")]
    public string? Message { get; set; }
}

public class QuoteRequestUpdateViewModel
{
    public int Id { get; set; }

    [Display(Name = "Tila")]
    public QuoteRequestStatus Status { get; set; }

    [StringLength(2000)]
    [Display(Name = "Muistiinpanot (sisäiset)")]
    public string? AdminNotes { get; set; }
}
