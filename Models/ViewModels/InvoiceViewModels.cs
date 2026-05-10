using System.ComponentModel.DataAnnotations;
using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HakaTech.Portal.Models.ViewModels;

/// <summary>Yksittäinen laskurivi laskun luontilomakkeessa.</summary>
public class InvoiceLineInputModel
{
    [Required(ErrorMessage = "Kuvaus on pakollinen.")]
    [StringLength(300)]
    [Display(Name = "Kuvaus")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 9999, ErrorMessage = "Määrä on oltava positiivinen.")]
    [Display(Name = "Määrä")]
    public decimal Quantity { get; set; } = 1;

    [Required]
    [Range(0, 999999, ErrorMessage = "Hinta on oltava positiivinen.")]
    [Display(Name = "À-hinta (€)")]
    public decimal UnitPrice { get; set; }
}

/// <summary>Laskun luontilomakkeen malli — sisältää laskun tiedot ja vähintään yhden rivin.</summary>
public class InvoiceCreateViewModel
{
    [Required(ErrorMessage = "Asiakas on valittava.")]
    [Display(Name = "Asiakas")]
    public int CustomerId { get; set; }
    public IEnumerable<SelectListItem> CustomerOptions { get; set; } = [];

    [Required(ErrorMessage = "Laskunumero on pakollinen.")]
    [StringLength(50)]
    [Display(Name = "Laskunumero")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Laskun päivämäärä")]
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Eräpäivä")]
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(14);

    [Range(0, 1, ErrorMessage = "ALV-prosentti 0–100 %.")]
    [Display(Name = "ALV-%")]
    public decimal VatRate { get; set; } = 0.255m;

    [StringLength(1000)]
    [Display(Name = "Muistiinpanot")]
    public string? Notes { get; set; }

    // Laskurivit – vähintään yksi vaaditaan
    public List<InvoiceLineInputModel> Lines { get; set; } =
        [new InvoiceLineInputModel()];
}

/// <summary>Pieni lomake laskun tilan muuttamiseen (esim. Sent → Paid).</summary>
public class InvoiceStatusViewModel
{
    public int Id { get; set; }

    [Display(Name = "Tila")]
    public InvoiceStatus Status { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Maksupäivä")]
    public DateTime? PaidAt { get; set; }
}
