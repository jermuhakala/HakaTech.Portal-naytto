namespace HakaTech.Portal.Services;

public interface IAuditService
{
    Task LogAsync(
        string  action,
        string? entityType = null,
        string? entityId   = null,
        string? details    = null);
}
