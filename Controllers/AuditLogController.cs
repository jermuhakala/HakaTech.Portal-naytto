using HakaTech.Portal.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Controllers;

[Authorize(Roles = "Admin")]
public class AuditLogController : Controller
{
    private readonly ApplicationDbContext _db;

    public AuditLogController(ApplicationDbContext db)
    {
        _db = db;
    }

    // ── GET /AuditLog ────────────────────────────────────────────────
    public async Task<IActionResult> Index(
        string? action,
        string? userEmail,
        string? entityType,
        DateTime? from,
        DateTime? to,
        int page = 1)
    {
        const int pageSize = 50;

        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action.Contains(action));

        if (!string.IsNullOrWhiteSpace(userEmail))
            query = query.Where(a => a.UserEmail != null && a.UserEmail.Contains(userEmail));

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.Timestamp < to.Value.AddDays(1));

        int total = await query.CountAsync();

        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page       = page;
        ViewBag.PageSize   = pageSize;
        ViewBag.Total      = total;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

        ViewBag.FilterAction     = action;
        ViewBag.FilterUserEmail  = userEmail;
        ViewBag.FilterEntityType = entityType;
        ViewBag.FilterFrom       = from?.ToString("yyyy-MM-dd");
        ViewBag.FilterTo         = to?.ToString("yyyy-MM-dd");

        // Distinct action types for filter dropdown
        ViewBag.ActionTypes = await _db.AuditLogs
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        return View(logs);
    }
}
