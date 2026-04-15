using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace HakaTech.Portal.Controllers;

[Authorize]
public class KnowledgeBaseController : Controller
{
    private readonly ApplicationDbContext         _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public KnowledgeBaseController(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager)
    {
        _db          = db;
        _userManager = userManager;
    }

    // ── GET /KnowledgeBase ───────────────────────────────────────────
    public async Task<IActionResult> Index(string? q, int? categoryId)
    {
        var categories = await _db.KnowledgeBaseCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync();

        var query = _db.KnowledgeBaseArticles
            .Include(a => a.Category)
            .Where(a => a.IsPublished && (a.Category == null || a.Category.IsActive));

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(a =>
                a.Title.Contains(q) || a.Content.Contains(q));

        if (categoryId.HasValue)
            query = query.Where(a => a.CategoryId == categoryId.Value);

        var articles = await query
            .OrderByDescending(a => a.IsFeatured)
            .ThenByDescending(a => a.UpdatedAt)
            .Select(a => new KbArticleCardViewModel
            {
                Id           = a.Id,
                Title        = a.Title,
                CategoryName = a.Category!.Name,
                CategoryId   = a.CategoryId,
                ViewCount    = a.ViewCount,
                IsFeatured   = a.IsFeatured,
                UpdatedAt    = a.UpdatedAt,
                Excerpt      = MakeExcerpt(a.Content, 150)
            })
            .ToListAsync();

        var featured = string.IsNullOrWhiteSpace(q) && !categoryId.HasValue
            ? articles.Where(a => a.IsFeatured).Take(4).ToList()
            : [];

        var model = new KbIndexViewModel
        {
            SearchQuery = q,
            CategoryId  = categoryId,
            Categories  = categories,
            Articles    = articles,
            Featured    = featured
        };

        return View(model);
    }

    // ── GET /KnowledgeBase/Article/5 ────────────────────────────────
    public async Task<IActionResult> Article(int id)
    {
        var article = await _db.KnowledgeBaseArticles
            .Include(a => a.Category)
            .Include(a => a.CreatedByUser)
            .FirstOrDefaultAsync(a => a.Id == id && a.IsPublished);

        if (article is null) return NotFound();

        // Kasvata katselukertaa
        article.ViewCount++;
        await _db.SaveChangesAsync();

        // Samaan kategoriaan kuuluvat muut artikkelit
        var related = await _db.KnowledgeBaseArticles
            .Include(a => a.Category)
            .Where(a => a.CategoryId == article.CategoryId && a.Id != id && a.IsPublished)
            .OrderByDescending(a => a.ViewCount)
            .Take(4)
            .Select(a => new KbArticleCardViewModel
            {
                Id           = a.Id,
                Title        = a.Title,
                CategoryName = a.Category!.Name,
                CategoryId   = a.CategoryId,
                ViewCount    = a.ViewCount,
                UpdatedAt    = a.UpdatedAt,
                Excerpt      = MakeExcerpt(a.Content, 100)
            })
            .ToListAsync();

        var model = new KbArticleDetailViewModel
        {
            Id           = article.Id,
            Title        = article.Title,
            Content      = article.Content,
            CategoryName = article.Category?.Name ?? "",
            CategoryId   = article.CategoryId,
            ViewCount    = article.ViewCount,
            UpdatedAt    = article.UpdatedAt,
            CreatedAt    = article.CreatedAt,
            Author       = article.CreatedByUser?.Email ?? "HakaTech",
            RelatedArticles = related
        };

        return View(model);
    }

    // ── GET /KnowledgeBase/Search?q=xxx  (JSON, tikettilomaketta varten) ──
    [HttpGet]
    public async Task<IActionResult> Search(string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Json(Array.Empty<object>());

        var results = await _db.KnowledgeBaseArticles
            .Include(a => a.Category)
            .Where(a => a.IsPublished &&
                        (a.Title.Contains(q) || a.Content.Contains(q)))
            .OrderByDescending(a => a.Title.Contains(q))
            .ThenByDescending(a => a.ViewCount)
            .Take(5)
            .Select(a => new KbSearchResultItem
            {
                Id       = a.Id,
                Title    = a.Title,
                Excerpt  = MakeExcerpt(a.Content, 100),
                Category = a.Category!.Name
            })
            .ToListAsync();

        return Json(results);
    }

    // ── GET /KnowledgeBase/Manage ────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Manage()
    {
        var articles = await _db.KnowledgeBaseArticles
            .Include(a => a.Category)
            .Include(a => a.CreatedByUser)
            .OrderBy(a => a.Category!.SortOrder)
            .ThenBy(a => a.Title)
            .ToListAsync();

        return View(articles);
    }

    // ── GET /KnowledgeBase/CreateArticle ────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateArticle(int? categoryId)
    {
        var model = new KbArticleFormViewModel
        {
            CategoryId      = categoryId ?? 0,
            CategoryOptions = await BuildCategoryOptionsAsync()
        };
        return View("ArticleForm", model);
    }

    // ── POST /KnowledgeBase/CreateArticle ───────────────────────────
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateArticle(KbArticleFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.CategoryOptions = await BuildCategoryOptionsAsync();
            return View("ArticleForm", model);
        }

        var user = await _userManager.GetUserAsync(User);
        var article = new KnowledgeBaseArticle
        {
            Title           = model.Title,
            Content         = model.Content,
            CategoryId      = model.CategoryId,
            IsPublished     = model.IsPublished,
            IsFeatured      = model.IsFeatured,
            CreatedByUserId = user!.Id,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow
        };

        _db.KnowledgeBaseArticles.Add(article);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Artikkeli \"{article.Title}\" luotu.";
        return RedirectToAction(nameof(Manage));
    }

    // ── GET /KnowledgeBase/EditArticle/5 ────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditArticle(int id)
    {
        var article = await _db.KnowledgeBaseArticles.FindAsync(id);
        if (article is null) return NotFound();

        var model = new KbArticleFormViewModel
        {
            Id              = article.Id,
            Title           = article.Title,
            Content         = article.Content,
            CategoryId      = article.CategoryId,
            IsPublished     = article.IsPublished,
            IsFeatured      = article.IsFeatured,
            CategoryOptions = await BuildCategoryOptionsAsync()
        };
        return View("ArticleForm", model);
    }

    // ── POST /KnowledgeBase/EditArticle/5 ───────────────────────────
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

        article.Title       = model.Title;
        article.Content     = model.Content;
        article.CategoryId  = model.CategoryId;
        article.IsPublished = model.IsPublished;
        article.IsFeatured  = model.IsFeatured;
        article.UpdatedAt   = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Artikkeli \"{article.Title}\" päivitetty.";
        return RedirectToAction(nameof(Manage));
    }

    // ── POST /KnowledgeBase/DeleteArticle/5 ─────────────────────────
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteArticle(int id)
    {
        var article = await _db.KnowledgeBaseArticles.FindAsync(id);
        if (article is null) return NotFound();

        _db.KnowledgeBaseArticles.Remove(article);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Artikkeli \"{article.Title}\" poistettu.";
        return RedirectToAction(nameof(Manage));
    }

    // ── GET /KnowledgeBase/Categories ───────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Categories()
    {
        var cats = await _db.KnowledgeBaseCategories
            .Include(c => c.Articles)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync();
        return View(cats);
    }

    // ── GET /KnowledgeBase/CreateCategory ───────────────────────────
    [Authorize(Roles = "Admin")]
    public IActionResult CreateCategory() =>
        View("CategoryForm", new KbCategoryFormViewModel());

    // ── POST /KnowledgeBase/CreateCategory ──────────────────────────
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(KbCategoryFormViewModel model)
    {
        if (!ModelState.IsValid) return View("CategoryForm", model);

        _db.KnowledgeBaseCategories.Add(new KnowledgeBaseCategory
        {
            Name        = model.Name,
            Description = model.Description,
            SortOrder   = model.SortOrder,
            IsActive    = model.IsActive
        });
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Kategoria \"{model.Name}\" luotu.";
        return RedirectToAction(nameof(Categories));
    }

    // ── GET /KnowledgeBase/EditCategory/5 ───────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditCategory(int id)
    {
        var cat = await _db.KnowledgeBaseCategories.FindAsync(id);
        if (cat is null) return NotFound();

        return View("CategoryForm", new KbCategoryFormViewModel
        {
            Id          = cat.Id,
            Name        = cat.Name,
            Description = cat.Description,
            SortOrder   = cat.SortOrder,
            IsActive    = cat.IsActive
        });
    }

    // ── POST /KnowledgeBase/EditCategory/5 ──────────────────────────
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, KbCategoryFormViewModel model)
    {
        if (!ModelState.IsValid) return View("CategoryForm", model);

        var cat = await _db.KnowledgeBaseCategories.FindAsync(id);
        if (cat is null) return NotFound();

        cat.Name        = model.Name;
        cat.Description = model.Description;
        cat.SortOrder   = model.SortOrder;
        cat.IsActive    = model.IsActive;

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Kategoria \"{cat.Name}\" päivitetty.";
        return RedirectToAction(nameof(Categories));
    }

    // ── POST /KnowledgeBase/DeleteCategory/5 ────────────────────────
    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var cat = await _db.KnowledgeBaseCategories
            .Include(c => c.Articles)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cat is null) return NotFound();

        if (cat.Articles.Count > 0)
        {
            TempData["ErrorMessage"] = $"Kategorian \"{cat.Name}\" alla on {cat.Articles.Count} artikkelia. Siirrä tai poista ne ensin.";
            return RedirectToAction(nameof(Categories));
        }

        _db.KnowledgeBaseCategories.Remove(cat);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Kategoria \"{cat.Name}\" poistettu.";
        return RedirectToAction(nameof(Categories));
    }

    // ── Apumetodit ────────────────────────────────────────────────────
    private async Task<IEnumerable<SelectListItem>> BuildCategoryOptionsAsync() =>
        (await _db.KnowledgeBaseCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync())
        .Select(c => new SelectListItem(c.Name, c.Id.ToString()));

    private static string MakeExcerpt(string html, int maxLength)
    {
        // Poistetaan HTML-tagit ja typistetään
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }
}
