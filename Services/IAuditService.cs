namespace HakaTech.Portal.Services;

/// <summary>
/// Audit-lokituspalvelu: tallentaa tärkeät toiminnot AuditLog-tauluun.
/// Käytetään tietoturvatutkintaan ja muutoshistorian tarkasteluun.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Kirjoittaa audit-merkinnän. Käyttäjä ja IP haetaan automaattisesti
    /// nykyisestä HttpContextista.
    /// </summary>
    /// <param name="action">Toiminto, esim. "Login", "TicketCreated".</param>
    /// <param name="entityType">Kohdetyyppi (esim. "Ticket"). Vapaaehtoinen.</param>
    /// <param name="entityId">Kohteen tunniste. Vapaaehtoinen.</param>
    /// <param name="details">Vapaa lisätieto, esim. vanha vs. uusi arvo.</param>
    Task LogAsync(
        string  action,
        string? entityType = null,
        string? entityId   = null,
        string? details    = null);
}
