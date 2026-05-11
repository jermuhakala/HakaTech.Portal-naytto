// Validointiattribuutit.
using System.ComponentModel.DataAnnotations;
// Domain-mallit.
using HakaTech.Portal.Models.Domain;
// SelectListItem pudotusvalikkoa varten.
using Microsoft.AspNetCore.Mvc.Rendering;

// Nimiavaruus.
namespace HakaTech.Portal.Models.ViewModels;

// ── Asiakkaan hakusivu ────────────────────────────────────────────────────────

/// <summary>Tietopankin etusivun malli — sisältää haun, kategorialistan ja artikkelit.</summary>
// Tätä käyttää KnowledgeBase/Index.cshtml.
// Sisältää kaiken mitä tietopankin etusivu tarvitsee: haun, kategoriat, artikkelit, nostetut.
public class KbIndexViewModel
{
    // Hakusana jos käyttäjä on etsinyt jotain. null = ei haettu.
    public string?  SearchQuery  { get; set; }
    // Valittu kategoria ID. null = kaikki kategoriat.
    public int?     CategoryId   { get; set; }

    // Lista kaikista aktiivisista kategorioista — näytetään vasemmassa valikossa.
    public List<KnowledgeBaseCategory> Categories { get; set; } = [];
    // Lista artikkeleista, joita näytetään päälistassa (suodatettu haun/kategorian mukaan).
    public List<KbArticleCardViewModel> Articles  { get; set; } = [];
    // Nostetut artikkelit (IsFeatured=true) etusivun yläosassa.
    // Tyhjenee kun hakusana tai kategoria on valittu.
    public List<KbArticleCardViewModel> Featured  { get; set; } = [];
}

/// <summary>Yhden artikkelin korttinäkymä listoissa.</summary>
// Pieni ViewModel — ei koko artikkelin sisältöä, vain tiedot listanäkymää varten.
// Näin ei haeta koko artikkelin HTML-sisältöä tietokannasta kun listataan kymmeniä artikkeleita.
public class KbArticleCardViewModel
{
    public int    Id           { get; set; }
    // Artikkelin otsikko.
    public string Title        { get; set; } = string.Empty;
    // Kategorian nimi — näytetään kortin alaosassa.
    public string CategoryName { get; set; } = string.Empty;
    // Kategorian ID — tarvitaan linkin rakentamiseen.
    public int    CategoryId   { get; set; }
    // Lukukerrat — näytetään "suosituimmat artikkelit" -listalla.
    public int    ViewCount    { get; set; }
    // Onko nostettu etusivulle.
    public bool   IsFeatured   { get; set; }
    // Viimeksi päivitetty — näytetään päivämääränä kortin oikeassa alanurkassa.
    public DateTime UpdatedAt  { get; set; }
    // Lyhyt esikatselu artikkelin sisällöstä ilman HTML-tageja, max 150 merkkiä.
    // Generoidaan MakeExcerpt()-metodilla controllerissa.
    public string Excerpt      { get; set; } = string.Empty;
}

// ── Artikkelin detaljisivu ────────────────────────────────────────────────────

/// <summary>Yksittäisen artikkelin detaljinäkymä (täysi sisältö + samaan kategoriaan liittyvät).</summary>
// Tätä käyttää KnowledgeBase/Article.cshtml.
// Sisältää täyden artikkelin sekä listan saman kategorian muista artikkeleista.
public class KbArticleDetailViewModel
{
    public int      Id           { get; set; }
    // Artikkelin otsikko.
    public string   Title        { get; set; } = string.Empty;
    // HTML-muotoinen sisältö — renderöidään @Html.Raw(Model.Content) -muodossa.
    // Sisältö on jo sanitoitu ennen tallennusta (XSS-suoja).
    public string   Content      { get; set; } = string.Empty;
    // Kategorian nimi — näytetään navigaation murupolkuna.
    public string   CategoryName { get; set; } = string.Empty;
    public int      CategoryId   { get; set; }
    // Lukukerrat — näytetään artikkelin alaosassa.
    public int      ViewCount    { get; set; }
    // Viimeksi päivitetty.
    public DateTime UpdatedAt    { get; set; }
    // Milloin artikkeli luotiin.
    public DateTime CreatedAt    { get; set; }
    // Kirjoittajan nimi tai sähköposti.
    public string   Author       { get; set; } = string.Empty;

    // Muut artikkelit samasta kategoriasta ("Katso myös" -osio).
    // Enintään 4 artikkelia — järjestetään lukukertojen mukaan.
    public List<KbArticleCardViewModel> RelatedArticles { get; set; } = [];
}

// ── Admin: artikkelilomake ────────────────────────────────────────────────────

/// <summary>Adminin artikkelin luonti- ja muokkauslomake.</summary>
// Dual-purpose: sekä Create (Id=0) että Edit (Id>0).
// Sisältökentässä käytetään WYSIWYG-editoria (Quill tai TipTap).
public class KbArticleFormViewModel
{
    /// <summary>Artikkelin id, 0 = uusi artikkeli.</summary>
    public int Id { get; set; }

    // Artikkelin otsikko. Pakollinen, enintään 300 merkkiä.
    [Required(ErrorMessage = "Otsikko on pakollinen.")]
    [StringLength(300, ErrorMessage = "Otsikko voi olla enintään 300 merkkiä.")]
    [Display(Name = "Otsikko")]
    public string Title { get; set; } = string.Empty;

    // Artikkelin sisältö. Pakollinen.
    // WYSIWYG-editori lähettää tämän HTML-merkkijonona.
    // Controller sanitoi ennen tallennusta.
    [Required(ErrorMessage = "Sisältö on pakollinen.")]
    [Display(Name = "Sisältö")]
    public string Content { get; set; } = string.Empty;

    // Kategoria on pakollinen — artikkeli täytyy kuulua johonkin kategoriaan.
    [Required(ErrorMessage = "Kategoria on valittava.")]
    [Display(Name = "Kategoria")]
    public int CategoryId { get; set; }

    // Julkaisukytkin — false = luonnos.
    [Display(Name = "Julkaistu (näkyy asiakkaille)")]
    public bool IsPublished { get; set; } = true;

    // Nostetaanko etusivulle.
    [Display(Name = "Nostettu (näkyy etusivulla)")]
    public bool IsFeatured { get; set; }

    // Kategoriapudotusvalikko. Täytetään controllerissa.
    public IEnumerable<SelectListItem> CategoryOptions { get; set; } = [];
}

// ── Admin: kategorialomake ────────────────────────────────────────────────────

/// <summary>Adminin kategorian luonti- ja muokkauslomake.</summary>
public class KbCategoryFormViewModel
{
    // 0 = uusi kategoria.
    public int Id { get; set; }

    // Kategorian nimi. Pakollinen, enintään 100 merkkiä.
    [Required(ErrorMessage = "Nimi on pakollinen.")]
    [StringLength(100, ErrorMessage = "Nimi voi olla enintään 100 merkkiä.")]
    [Display(Name = "Kategorian nimi")]
    public string Name { get; set; } = string.Empty;

    // Lyhyt kuvaus kategorian sisällöstä. Vapaaehtoinen.
    [StringLength(500)]
    [Display(Name = "Kuvaus")]
    public string? Description { get; set; }

    // Järjestysnumero — pienempi numero → ensin listalla.
    [Display(Name = "Järjestysnumero")]
    public int SortOrder { get; set; }

    // Onko kategoria aktiivinen (näkyvissä).
    [Display(Name = "Aktiivinen")]
    public bool IsActive { get; set; } = true;
}

// ── JSON-hakuvastaus tikettilomakkeelle ──────────────────────────────────────

/// <summary>
/// Tiketin luontilomakkeen JSON-haun tulosrivi: kun käyttäjä kirjoittaa
/// otsikkoa tiketille, ehdotetaan ratkaisuja tietopankista.
/// </summary>
// Tätä käytetään KnowledgeBaseController.Search()-metodissa
// joka palauttaa JSON-vastauksen tiketin luontilomakkeen debounce-hausta.
// JavaScript näyttää tulokset lomakkeen alla.
public class KbSearchResultItem
{
    // Artikkelin ID — käytetään linkin rakentamiseen.
    public int    Id      { get; set; }
    // Artikkelin otsikko — näytetään hakutuloksessa.
    public string Title   { get; set; } = string.Empty;
    // Lyhyt esikatselu — näytetään otsikon alla.
    public string Excerpt { get; set; } = string.Empty;
    // Kategorian nimi — näytetään kursiivilla otsikon vieressä.
    public string Category { get; set; } = string.Empty;
}
