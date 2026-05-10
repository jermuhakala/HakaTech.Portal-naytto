using System.ComponentModel.DataAnnotations;
using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HakaTech.Portal.Models.ViewModels;

// ── Asiakkaan hakusivu ────────────────────────────────────────────────

/// <summary>Tietopankin etusivun malli — sisältää haun, kategorialistan ja artikkelit.</summary>
public class KbIndexViewModel
{
    public string?  SearchQuery  { get; set; }
    public int?     CategoryId   { get; set; }

    public List<KnowledgeBaseCategory> Categories { get; set; } = [];
    public List<KbArticleCardViewModel> Articles  { get; set; } = [];
    public List<KbArticleCardViewModel> Featured  { get; set; } = [];
}

/// <summary>Yhden artikkelin korttinäkymä listoissa.</summary>
public class KbArticleCardViewModel
{
    public int    Id           { get; set; }
    public string Title        { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int    CategoryId   { get; set; }
    public int    ViewCount    { get; set; }
    public bool   IsFeatured   { get; set; }
    public DateTime UpdatedAt  { get; set; }
    public string Excerpt      { get; set; } = string.Empty;  // teksti ilman HTML-tageja, max 150 merkkiä
}

// ── Artikkelin detaljisivu ───────────────────────────────────────────

/// <summary>Yksittäisen artikkelin detaljinäkymä (täysi sisältö + samaan kategoriaan liittyvät).</summary>
public class KbArticleDetailViewModel
{
    public int      Id           { get; set; }
    public string   Title        { get; set; } = string.Empty;
    public string   Content      { get; set; } = string.Empty;
    public string   CategoryName { get; set; } = string.Empty;
    public int      CategoryId   { get; set; }
    public int      ViewCount    { get; set; }
    public DateTime UpdatedAt    { get; set; }
    public DateTime CreatedAt    { get; set; }
    public string   Author       { get; set; } = string.Empty;

    public List<KbArticleCardViewModel> RelatedArticles { get; set; } = [];
}

// ── Admin: artikkelilomake ───────────────────────────────────────────

/// <summary>Adminin artikkelin luonti- ja muokkauslomake.</summary>
public class KbArticleFormViewModel
{
    /// <summary>Artikkelin id, 0 = uusi artikkeli.</summary>
    public int Id { get; set; }

    [Required(ErrorMessage = "Otsikko on pakollinen.")]
    [StringLength(300, ErrorMessage = "Otsikko voi olla enintään 300 merkkiä.")]
    [Display(Name = "Otsikko")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sisältö on pakollinen.")]
    [Display(Name = "Sisältö")]
    public string Content { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kategoria on valittava.")]
    [Display(Name = "Kategoria")]
    public int CategoryId { get; set; }

    [Display(Name = "Julkaistu (näkyy asiakkaille)")]
    public bool IsPublished { get; set; } = true;

    [Display(Name = "Nostettu (näkyy etusivulla)")]
    public bool IsFeatured { get; set; }

    public IEnumerable<SelectListItem> CategoryOptions { get; set; } = [];
}

// ── Admin: kategorialomake ───────────────────────────────────────────

/// <summary>Adminin kategorian luonti- ja muokkauslomake.</summary>
public class KbCategoryFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Nimi on pakollinen.")]
    [StringLength(100, ErrorMessage = "Nimi voi olla enintään 100 merkkiä.")]
    [Display(Name = "Kategorian nimi")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Kuvaus")]
    public string? Description { get; set; }

    [Display(Name = "Järjestysnumero")]
    public int SortOrder { get; set; }

    [Display(Name = "Aktiivinen")]
    public bool IsActive { get; set; } = true;
}

// ── JSON-hakuvastaus tikettilomakkeelle ──────────────────────────────

/// <summary>
/// Tiketin luontilomakkeen JSON-haun tulosrivi: kun käyttäjä kirjoittaa
/// otsikkoa tiketille, ehdotetaan ratkaisuja tietopankista.
/// </summary>
public class KbSearchResultItem
{
    public int    Id      { get; set; }
    public string Title   { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
