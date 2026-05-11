// Nimiavaruuksien tuonnit.
using HakaTech.Portal.Data;                        // ApplicationDbContext — tietokantayhteys.
using HakaTech.Portal.Models.Domain;               // KnowledgeBaseArticle, KnowledgeBaseCategory...
using HakaTech.Portal.Models.ViewModels;           // KbIndexViewModel, KbArticleFormViewModel...
using HakaTech.Portal.Services;                    // IHtmlSanitizerService — XSS-suoja.
using Microsoft.AspNetCore.Authorization;          // [Authorize]-attribuutti.
using Microsoft.AspNetCore.Identity;               // UserManager.
using Microsoft.AspNetCore.Mvc;                    // Controller, IActionResult, Json...
using Microsoft.AspNetCore.Mvc.Rendering;          // SelectListItem — pudotusvalikon vaihtoehto.
using Microsoft.EntityFrameworkCore;               // Include, ToListAsync, FindAsync...
using System.Text.RegularExpressions;              // Regex.Replace — HTML-tagien poisto esikatselua varten.

// Nimiavaruus.
namespace HakaTech.Portal.Controllers;

/// <summary>
/// Tietopankin controller. Asiakaskäyttäjille se on self-service-ohjepankki
/// (Index, Article), adminille kategorioiden ja artikkelien hallintapaneeli.
/// HTML-sisältö sanitoidaan ennen tallennusta XSS-suojan vuoksi.
/// </summary>
// [Authorize] = kirjautuminen vaaditaan kaikkiin toimintoihin.
// Julkinen tietopankki vaatii silti kirjautumisen — sisältö on asiakaskohtaista.
[Authorize]
public class KnowledgeBaseController : Controller
{
    // Tietokantayhteys.
    private readonly ApplicationDbContext         _db;
    // UserManager — haetaan kirjautunut admin artikkelin luomista varten.
    private readonly UserManager<ApplicationUser> _userManager;
    // HTML-sanitoija — puhdistaa vaarallisen koodin WYSIWYG-editorin tuottamasta HTML:stä.
    // XSS (Cross-Site Scripting): hyökkääjä voisi upottaa haitallista JS-koodia artikkelin sisältöön.
    private readonly IHtmlSanitizerService        _sanitizer;

    // Konstruktori: DI-säiliö täyttää parametrit.
    public KnowledgeBaseController(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager,
        IHtmlSanitizerService        sanitizer)
    {
        _db          = db;
        _userManager = userManager;
        _sanitizer   = sanitizer;
    }

    // ── GET /KnowledgeBase ───────────────────────────────────────────────────
    // Tietopankin etusivu: hakutoiminto, kategorialista ja artikkelijulista.
    public async Task<IActionResult> Index(
        string? q,          // Hakusana. null = ei haeta.
        int? categoryId)    // Kategoriasuodatin. null = kaikki kategoriat.
    {
        // Haetaan kaikki aktiiviset kategoriat vasemman sivupalkin valikkoa varten.
        // Järjestyksessä: ensin SortOrder (pienin luku = ylös), sitten nimi aakkosjärjestyksessä.
        var categories = await _db.KnowledgeBaseCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync();

        // Aloitetaan artikkelikysely — näytetään vain julkaistut artikkelit aktiivisissa kategorioissa.
        // Include(a => a.Category) = ladataan kategorian tiedot (nimi).
        var query = _db.KnowledgeBaseArticles
            .Include(a => a.Category)
            .Where(a => a.IsPublished && (a.Category == null || a.Category.IsActive));
        // a.Category == null OR a.Category.IsActive: jos artikkeli ei kuulu kategoriaan, se näytetään silti.

        // Hakusuodatin — jos hakusana on annettu, haetaan otsikosta ja sisällöstä.
        // Contains() = SQL:n LIKE '%hakusana%' — osittainen vastaavuus.
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(a =>
                a.Title.Contains(q) || a.Content.Contains(q));

        // Kategoriasuodatin.
        if (categoryId.HasValue)
            query = query.Where(a => a.CategoryId == categoryId.Value);

        // Haetaan artikkelit ja muunnetaan samalla KbArticleCardViewModel-tyyppisiksi.
        // Nostetut artikkelit (IsFeatured) ensin, sitten uusimmasta vanhimpaan.
        var articles = await query
            .OrderByDescending(a => a.IsFeatured)  // true > false: nostetut ensin.
            .ThenByDescending(a => a.UpdatedAt)    // Uusin päivitys ensin.
            // Select() = muunnetaan suoraan SQL-kyselyssä — ei haeta koko artikkelin sisältöä.
            // Näin säästetään muistia ja verkkoa (Content voi olla megabittejä HTML:ää).
            .Select(a => new KbArticleCardViewModel
            {
                Id           = a.Id,
                Title        = a.Title,
                // Ternary: jos kategoriaa ei ole, näytetään tyhjä merkkijono.
                CategoryName = a.Category != null ? a.Category.Name : "",
                CategoryId   = a.CategoryId,
                ViewCount    = a.ViewCount,  // Lukukerrat "suosituimmat" -järjestykseen.
                IsFeatured   = a.IsFeatured,
                UpdatedAt    = a.UpdatedAt,
                // Esikatselu: poistetaan HTML-tagit ja typistetään 150 merkkiin.
                // MakeExcerpt on yksityinen apumetodi tämän tiedoston alareunassa.
                Excerpt      = MakeExcerpt(a.Content, 150)
            })
            .ToListAsync();

        // Nostetut artikkelit etusivun yläosaan — vain jos ei haeta eikä suodata.
        // Jos käyttäjä hakee tai suodattaa, nostetut piilotetaan (ei sekoita hakutuloksia).
        // "[]" = tyhjä lista (C# 12:n collection expression).
        var featured = string.IsNullOrWhiteSpace(q) && !categoryId.HasValue
            ? articles.Where(a => a.IsFeatured).Take(4).ToList() // Max 4 nostettua.
            : [];

        // Rakennetaan ViewModel kaikilla tiedoilla.
        var model = new KbIndexViewModel
        {
            SearchQuery = q,           // Hakusana lomakkeen esitäyttämiseen.
            CategoryId  = categoryId,  // Valittu kategoria korostusta varten.
            Categories  = categories,  // Sivupalkin kategoriat.
            Articles    = articles,    // Päälistauksen artikkelit.
            Featured    = featured     // Nostetut artikkelit (tyhjä haettaessa).
        };

        return View(model);
    }

    // ── GET /KnowledgeBase/Article/5 ────────────────────────────────────────
    // Yksittäisen artikkelin näyttäminen täydellä sisällöllä.
    public async Task<IActionResult> Article(int id)
    {
        // Haetaan artikkeli kategorian ja kirjoittajan tiedoilla.
        // && a.IsPublished: ei näytetä julkaisemattomia (admin voi nähdä ne Manage-sivulla).
        var article = await _db.KnowledgeBaseArticles
            .Include(a => a.Category)
            .Include(a => a.CreatedByUser) // Kirjoittajan nimi/sähköposti.
            .FirstOrDefaultAsync(a => a.Id == id && a.IsPublished);

        if (article is null) return NotFound();

        // Kasvatetaan lukukertalaskuria — jokaisesta vierailusta kasvaa yhdellä.
        // Tätä käytetään "suosituimmat artikkelit" -järjestykseen.
        article.ViewCount++;
        await _db.SaveChangesAsync(); // UPDATE SQL.

        // Haetaan saman kategorian muut artikkelit "Katso myös" -osiota varten.
        // Järjestetään lukukertojen mukaan — suosituimmat ensin.
        var related = await _db.KnowledgeBaseArticles
            .Include(a => a.Category)
            .Where(a => a.CategoryId == article.CategoryId // Sama kategoria.
                     && a.Id != id                          // Ei sama artikkeli.
                     && a.IsPublished)                      // Vain julkaistut.
            .OrderByDescending(a => a.ViewCount)
            .Take(4) // Max 4 liittyvää artikkelia.
            .Select(a => new KbArticleCardViewModel
            {
                Id           = a.Id,
                Title        = a.Title,
                CategoryName = a.Category != null ? a.Category.Name : "",
                CategoryId   = a.CategoryId,
                ViewCount    = a.ViewCount,
                UpdatedAt    = a.UpdatedAt,
                Excerpt      = MakeExcerpt(a.Content, 100) // Lyhyempi esikatselussa.
            })
            .ToListAsync();

        // Rakennetaan täyden artikkelin ViewModel.
        var model = new KbArticleDetailViewModel
        {
            Id           = article.Id,
            Title        = article.Title,
            Content      = article.Content, // Täysi sanitoitu HTML-sisältö.
            // "??" = jos kategoriaa ei ole (artikkeli ei kuulu mihinkään), käytetään tyhjää merkkijonoa.
            CategoryName = article.Category?.Name ?? "",
            CategoryId   = article.CategoryId,
            ViewCount    = article.ViewCount, // Päivitetty arvo (yllä kasvatettu).
            UpdatedAt    = article.UpdatedAt,
            CreatedAt    = article.CreatedAt,
            // Kirjoittajan sähköposti tai oletusarvo "HakaTech" jos käyttäjää ei löydy.
            Author       = article.CreatedByUser?.Email ?? "HakaTech",
            RelatedArticles = related
        };

        return View(model);
    }

    // ── GET /KnowledgeBase/Search?q=xxx ─────────────────────────────────────
    // JSON-hakurajapinta tiketin luontilomaketta varten.
    // Kun käyttäjä kirjoittaa tiketin otsikkoa, JS hakee tästä vastaavia artikkeleita.
    // Tavoite: "hae itse ennen kuin luot tiketin" — vähentää turhat tiketit.
    [HttpGet]
    public async Task<IActionResult> Search(string? q)
    {
        // Liian lyhyellä hakusanalla ei tehdä tietokantakutsua — palautetaan tyhjä lista.
        // Array.Empty<object>() = tyhjä taulukko (JSON: []).
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Json(Array.Empty<object>());

        // Haetaan vastaavat artikkelit: otsikko tai sisältö sisältää hakusanan.
        // Max 5 tulosta — JSON-vastauksesta ei tehdä isoja listauksia.
        var results = await _db.KnowledgeBaseArticles
            .Include(a => a.Category)
            .Where(a => a.IsPublished &&
                        (a.Title.Contains(q) || a.Content.Contains(q)))
            // OrderByDescending(a.Title.Contains(q)) = otsikkoosuma parempi kuin sisältöosuma.
            // EF Core kääntää tämän SQL:ksi: ORDER BY CASE WHEN Title LIKE '%q%' THEN 1 ELSE 0 END DESC.
            .OrderByDescending(a => a.Title.Contains(q))
            .ThenByDescending(a => a.ViewCount) // Sama prioriteetti → suositumpi ensin.
            .Take(5)
            .Select(a => new KbSearchResultItem
            {
                Id       = a.Id,
                Title    = a.Title,
                Excerpt  = MakeExcerpt(a.Content, 100), // Lyhyt esikatselu pudotuslistaan.
                Category = a.Category != null ? a.Category.Name : ""
            })
            .ToListAsync();

        // Json() = palauttaa JSON-muotoisen HTTP-vastauksen (ei HTML-näkymää).
        // JavaScript lukee tämän vastauksen ja näyttää ehdotukset lomakkeessa.
        return Json(results);
    }

    // ── GET /KnowledgeBase/Manage ────────────────────────────────────────────
    // Adminin hallintapaneeli: kaikki artikkelit kategorioittain.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Manage()
    {
        // Haetaan kaikki artikkelit (myös julkaisemattomat) admin-näkymää varten.
        // Include(a.Category) = kategorian nimi taulukkoon.
        // Include(a.CreatedByUser) = kirjoittajan sähköposti.
        var articles = await _db.KnowledgeBaseArticles
            .Include(a => a.Category)
            .Include(a => a.CreatedByUser)
            // Järjestys: ensin kategorian SortOrder, sitten artikkelin otsikko aakkosjärjestyksessä.
            // "!" = null-forgiving: Category ei ole null kun Include() on käytetty (kehittäjän takuu).
            .OrderBy(a => a.Category!.SortOrder)
            .ThenBy(a => a.Title)
            .ToListAsync();

        return View(articles);
    }

    // ── GET /KnowledgeBase/CreateArticle ────────────────────────────────────
    // Tyhjä artikkelilomake. Voidaan esivalita kategoria URL-parametrilla.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateArticle(
        int? categoryId) // Valinnainen: esivalittu kategoria pudotusvalikossa.
    {
        var model = new KbArticleFormViewModel
        {
            CategoryId      = categoryId ?? 0, // 0 = ei valittu.
            CategoryOptions = await BuildCategoryOptionsAsync() // Pudotusvalikon vaihtoehdot.
        };
        // Käytetään yhteistä "ArticleForm"-näkymää sekä luomiseen että muokkaamiseen.
        return View("ArticleForm", model);
    }

    // ── POST /KnowledgeBase/CreateArticle ───────────────────────────────────
    // Tallentaa uuden artikkelin — HTML sanitoidaan ennen tallentamista.
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateArticle(KbArticleFormViewModel model)
    {
        // Validoidaan lomake (otsikko, sisältö, kategoria pakolliset).
        if (!ModelState.IsValid)
        {
            model.CategoryOptions = await BuildCategoryOptionsAsync();
            return View("ArticleForm", model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var article = new KnowledgeBaseArticle
        {
            Title           = model.Title,
            // _sanitizer.Sanitize() = puhdistaa WYSIWYG-editorin tuottaman HTML:n.
            // Poistaa vaaralliset tagit (<script>, <iframe>) ja attribuutit (onerror, onclick).
            // XSS-suoja: ilman sanitointia hyökkääjä voisi lisätä haitallista JavaScriptia.
            Content         = _sanitizer.Sanitize(model.Content),
            CategoryId      = model.CategoryId,
            IsPublished     = model.IsPublished, // false = luonnos, ei näy asiakkaille.
            IsFeatured      = model.IsFeatured,  // true = nostetaan etusivulle.
            CreatedByUserId = user.Id,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow    // Luontihetkellä sama kuin CreatedAt.
        };

        _db.KnowledgeBaseArticles.Add(article);
        await _db.SaveChangesAsync(); // INSERT SQL.

        TempData["SuccessMessage"] = $"Artikkeli \"{article.Title}\" luotu.";
        return RedirectToAction(nameof(Manage));
    }

    // ── GET /KnowledgeBase/EditArticle/5 ────────────────────────────────────
    // Muokkauslomake olemassaolevalle artikkelille.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditArticle(int id)
    {
        var article = await _db.KnowledgeBaseArticles.FindAsync(id);
        if (article is null) return NotFound();

        // Täytetään ViewModel olemassaolevasta artikkelista.
        var model = new KbArticleFormViewModel
        {
            Id              = article.Id,          // Tarvitaan tunnistamaan muokattava artikkeli POST:ssa.
            Title           = article.Title,
            Content         = article.Content,     // Palautetaan raaakäsitelty (sanitoitu) HTML editoriin.
            CategoryId      = article.CategoryId,
            IsPublished     = article.IsPublished,
            IsFeatured      = article.IsFeatured,
            CategoryOptions = await BuildCategoryOptionsAsync()
        };
        return View("ArticleForm", model);
    }

    // ── POST /KnowledgeBase/EditArticle/5 ───────────────────────────────────
    // Tallentaa muokatun artikkelin — HTML sanitoidaan jälleen.
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditArticle(int id, KbArticleFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.CategoryOptions = await BuildCategoryOptionsAsync();
            return View("ArticleForm", model);
        }

        var article = await _db.KnowledgeBaseArticles.FindAsync(id);
        if (article is null) return NotFound();

        // Päivitetään muuttuneet kentät.
        article.Title       = model.Title;
        // HTML sanitoidaan jälleen vaikka sisältö ei muuttuisi — varmuuden vuoksi.
        article.Content     = _sanitizer.Sanitize(model.Content);
        article.CategoryId  = model.CategoryId;
        article.IsPublished = model.IsPublished;
        article.IsFeatured  = model.IsFeatured;
        // Päivitä muokkausaika — näytetään artikkelin alaosassa "Viimeksi päivitetty".
        article.UpdatedAt   = DateTime.UtcNow;

        await _db.SaveChangesAsync(); // UPDATE SQL.

        TempData["SuccessMessage"] = $"Artikkeli \"{article.Title}\" päivitetty.";
        return RedirectToAction(nameof(Manage));
    }

    // ── POST /KnowledgeBase/DeleteArticle/5 ─────────────────────────────────
    // Poistaa artikkelin pysyvästi tietokannasta.
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteArticle(int id)
    {
        var article = await _db.KnowledgeBaseArticles.FindAsync(id);
        if (article is null) return NotFound();

        _db.KnowledgeBaseArticles.Remove(article);
        await _db.SaveChangesAsync(); // DELETE SQL.

        TempData["SuccessMessage"] = $"Artikkeli \"{article.Title}\" poistettu.";
        return RedirectToAction(nameof(Manage));
    }

    // ── GET /KnowledgeBase/Categories ───────────────────────────────────────
    // Admin näkee kaikkien kategorioiden listan artikkelimäärillä.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Categories()
    {
        var cats = await _db.KnowledgeBaseCategories
            .Include(c => c.Articles) // Tarvitaan artikkelimäärän laskemiseen.
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync();
        return View(cats);
    }

    // ── GET /KnowledgeBase/CreateCategory ───────────────────────────────────
    // Tyhjä kategorian luontilomake.
    [Authorize(Roles = "Admin")]
    // Yksirivinen metodi: => View(...) on lyhenne yksinkertaiselle palautukselle.
    public IActionResult CreateCategory() =>
        View("CategoryForm", new KbCategoryFormViewModel());

    // ── POST /KnowledgeBase/CreateCategory ──────────────────────────────────
    // Tallentaa uuden kategorian.
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(KbCategoryFormViewModel model)
    {
        // Validoidaan lomake (nimi pakollinen).
        if (!ModelState.IsValid) return View("CategoryForm", model);

        // Luodaan uusi kategoria suoraan ViewModelista — ei tarvita erillistä ViewModel→entity -muunnosta
        // koska KbCategoryFormViewModel vastaa täysin KnowledgeBaseCategory-entiteettiä.
        _db.KnowledgeBaseCategories.Add(new KnowledgeBaseCategory
        {
            Name        = model.Name,
            Description = model.Description,
            SortOrder   = model.SortOrder, // Järjestysnumero näytössä.
            IsActive    = model.IsActive   // false = piilotettu asiakkailta.
        });
        await _db.SaveChangesAsync(); // INSERT SQL.

        TempData["SuccessMessage"] = $"Kategoria \"{model.Name}\" luotu.";
        return RedirectToAction(nameof(Categories));
    }

    // ── GET /KnowledgeBase/EditCategory/5 ───────────────────────────────────
    // Muokkauslomake olemassaolevalle kategorialle.
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditCategory(int id)
    {
        var cat = await _db.KnowledgeBaseCategories.FindAsync(id);
        if (cat is null) return NotFound();

        // Käytetään samaa "CategoryForm"-näkymää kuin luomisessa.
        return View("CategoryForm", new KbCategoryFormViewModel
        {
            Id          = cat.Id,          // Tarvitaan tunnistamaan muokattava kategoria POST:ssa.
            Name        = cat.Name,
            Description = cat.Description,
            SortOrder   = cat.SortOrder,
            IsActive    = cat.IsActive
        });
    }

    // ── POST /KnowledgeBase/EditCategory/5 ──────────────────────────────────
    // Tallentaa muokatun kategorian.
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, KbCategoryFormViewModel model)
    {
        if (!ModelState.IsValid) return View("CategoryForm", model);

        var cat = await _db.KnowledgeBaseCategories.FindAsync(id);
        if (cat is null) return NotFound();

        // Päivitetään kentät.
        cat.Name        = model.Name;
        cat.Description = model.Description;
        cat.SortOrder   = model.SortOrder;
        cat.IsActive    = model.IsActive;

        await _db.SaveChangesAsync(); // UPDATE SQL.

        TempData["SuccessMessage"] = $"Kategoria \"{cat.Name}\" päivitetty.";
        return RedirectToAction(nameof(Categories));
    }

    // ── POST /KnowledgeBase/DeleteCategory/5 ────────────────────────────────
    // Poistaa kategorian — mutta vain jos sillä ei ole artikkeleita.
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        // Ladataan kategoria artikkeleineen — tarvitaan tarkistukseen.
        var cat = await _db.KnowledgeBaseCategories
            .Include(c => c.Articles)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cat is null) return NotFound();

        // Turvallisuustarkistus: ei poisteta kategoriaa jos sillä on artikkeleita.
        // Syy: poistaminen rikkoisi viittaukset (artikkelissa olisi CategoryId viittaamassa poistettuun).
        // Parempi vaihtoehto: siirrä artikkelit toiseen kategoriaan tai poista ne ensin.
        if (cat.Articles.Count > 0)
        {
            TempData["ErrorMessage"] =
                $"Kategorian \"{cat.Name}\" alla on {cat.Articles.Count} artikkelia. " +
                "Siirrä tai poista ne ensin.";
            return RedirectToAction(nameof(Categories));
        }

        _db.KnowledgeBaseCategories.Remove(cat);
        await _db.SaveChangesAsync(); // DELETE SQL.

        TempData["SuccessMessage"] = $"Kategoria \"{cat.Name}\" poistettu.";
        return RedirectToAction(nameof(Categories));
    }

    // ── Yksityiset apumetodit ────────────────────────────────────────────────

    // Rakentaa pudotusvalikon aktiivisista kategorioista.
    // Käytetään CreateArticle- ja EditArticle-lomakkeissa.
    private async Task<IEnumerable<SelectListItem>> BuildCategoryOptionsAsync() =>
        (await _db.KnowledgeBaseCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync())
        // SelectListItem(näytettävä teksti, lomakkeelle lähetettävä arvo).
        .Select(c => new SelectListItem(c.Name, c.Id.ToString()));

    // Luo esikatselutekstin artikkelin HTML-sisällöstä.
    // "private static" = ei tarvita instanssia, puhdas funktio ilman sivuvaikutuksia.
    // Parametrit: html = artikkelin raakas HTML-sisältö, maxLength = enimmäispituus merkkeinä.
    private static string MakeExcerpt(string html, int maxLength)
    {
        // Vaihe 1: Poistetaan kaikki HTML-tagit säännöllisellä lausekkeella.
        // <[^>]+> = mikä tahansa tagi: "<" + yksi tai useampi ei-">"-merkki + ">".
        // Esim. "<p>Teksti</p>" → " Teksti " (tagit korvataan välilyönnillä).
        var text = Regex.Replace(html, "<[^>]+>", " ");
        // Vaihe 2: Tiivistetään useammat peräkkäiset välilyönnit yhdeksi.
        // \s+ = yksi tai useampi tyhjämerkki (välilyönti, rivinvaihto, tabulaattori).
        // Trim() = poistetaan alun ja lopun välilyönnit.
        text = Regex.Replace(text, @"\s+", " ").Trim();
        // Jos teksti on lyhyempi kuin raja, palautetaan se sellaisenaan.
        // Muuten: text[..maxLength] = ensimmäiset maxLength merkkiä (range-syntaksi, C# 8+).
        // TrimEnd() = poistetaan mahdollinen viimeinen välilyönti ennen "…" -merkkiä.
        // "…" (kolme pistettä yhdessä merkissä) kertoo lukijalle teksti jatkuu.
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }
}
