using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Identity;

namespace HakaTech.Portal.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext         _db;
    private readonly IHttpContextAccessor         _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuditService(
        ApplicationDbContext         db,
        IHttpContextAccessor         httpContextAccessor,
        UserManager<ApplicationUser> userManager)
    {
        _db                  = db;
        _httpContextAccessor = httpContextAccessor;
        _userManager         = userManager;
    }

    public async Task LogAsync(
        string  action,
        string? entityType = null,
        string? entityId   = null,
        string? details    = null)
    {
        var ctx   = _httpContextAccessor.HttpContext;
        var user  = ctx?.User;

        string? userId    = null;
        string? userEmail = null;

        if (user?.Identity?.IsAuthenticated == true)
        {
            var appUser = await _userManager.GetUserAsync(user);
            userId    = appUser?.Id;
            userEmail = appUser?.Email;
        }

        string? ip = ctx?.Connection?.RemoteIpAddress?.ToString();

        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp  = DateTime.UtcNow,
            UserId     = userId,
            UserEmail  = userEmail,
            Action     = action,
            EntityType = entityType,
            EntityId   = entityId,
            Details    = details,
            IpAddress  = ip
        });

        await _db.SaveChangesAsync();
    }
}
