using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

[ApiController]
[Authorize]
[Route("api/search")]
public sealed class SearchController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public SearchController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public sealed class SearchHit
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Subtitle { get; init; }
        public string Url { get; init; } = string.Empty;
        public string? Status { get; init; }
    }

    public sealed class SearchResultsDto
    {
        public List<SearchHit> Tickets   { get; set; } = new();
        public List<SearchHit> Invoices  { get; set; } = new();
        public List<SearchHit> Customers { get; set; } = new();
        public List<SearchHit> KbArticles { get; set; } = new();
    }

    [HttpGet]
    public async Task<ActionResult<SearchResultsDto>> Get([FromQuery] string? q, [FromQuery] int take = 5)
    {
        var result = new SearchResultsDto();
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return Ok(result);
        take = Math.Clamp(take, 1, 10);

        var me = await _users.GetUserAsync(User);
        var isAdmin = User.IsInRole("Admin");
        int? scopeCustomerId = isAdmin ? null : me?.CustomerId;
        var term = q.Trim();

        // Tickets
        var ticketsQ = _db.Tickets.AsNoTracking()
            .Include(t => t.Customer)
            .Where(t => scopeCustomerId == null || t.CustomerId == scopeCustomerId)
            .Where(t => EF.Functions.Like(t.Title, $"%{term}%")
                     || EF.Functions.Like(t.Description ?? "", $"%{term}%"));
        result.Tickets = await ticketsQ
            .OrderByDescending(t => t.CreatedAt)
            .Take(take)
            .Select(t => new SearchHit
            {
                Id = t.Id,
                Title = t.Title,
                Subtitle = t.Customer != null ? t.Customer.CompanyName : null,
                Url = $"/Ticket/Details/{t.Id}",
                Status = t.Status.ToString()
            })
            .ToListAsync();

        // Invoices
        var invoicesQ = _db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => scopeCustomerId == null || i.CustomerId == scopeCustomerId)
            .Where(i => EF.Functions.Like(i.InvoiceNumber, $"%{term}%"));
        result.Invoices = await invoicesQ
            .OrderByDescending(i => i.InvoiceDate)
            .Take(take)
            .Select(i => new SearchHit
            {
                Id = i.Id,
                Title = i.InvoiceNumber,
                Subtitle = i.Customer != null ? i.Customer.CompanyName : null,
                Url = $"/Invoice/Details/{i.Id}",
                Status = i.Status.ToString()
            })
            .ToListAsync();

        // Customers (admin-only)
        if (isAdmin)
        {
            result.Customers = await _db.Customers.AsNoTracking()
                .Where(c => EF.Functions.Like(c.CompanyName, $"%{term}%")
                         || EF.Functions.Like(c.BusinessId ?? "", $"%{term}%"))
                .OrderBy(c => c.CompanyName)
                .Take(take)
                .Select(c => new SearchHit
                {
                    Id = c.Id,
                    Title = c.CompanyName,
                    Subtitle = c.BusinessId,
                    Url = $"/Customer/Details/{c.Id}",
                    Status = c.IsActive ? "Active" : "Inactive"
                })
                .ToListAsync();
        }

        // KB articles
        result.KbArticles = await _db.KnowledgeBaseArticles.AsNoTracking()
            .Where(a => a.IsPublished)
            .Where(a => EF.Functions.Like(a.Title, $"%{term}%")
                     || EF.Functions.Like(a.Content ?? "", $"%{term}%"))
            .OrderByDescending(a => a.UpdatedAt)
            .Take(take)
            .Select(a => new SearchHit
            {
                Id = a.Id,
                Title = a.Title,
                Subtitle = null,
                Url = $"/KnowledgeBase/Details/{a.Id}"
            })
            .ToListAsync();

        return Ok(result);
    }
}
