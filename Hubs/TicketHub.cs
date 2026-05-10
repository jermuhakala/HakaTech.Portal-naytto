using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using HakaTech.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Hubs;

/// <summary>
/// SignalR-hub tikettien reaaliaikaiseen kommunikointiin. Kun käyttäjät
/// avaavat tiketin sivun, selain liittyy tiketin "huoneeseen" ja saa
/// uudet kommentit heti ilman sivun päivitystä.
///
/// Kanavarakenne:
///  - "Ticket_{id}"        → kaikki tiketin osallistujat (asiakas + admin)
///  - "Ticket_{id}_Admin"  → vain admin (sisäiset muistiinpanot)
/// </summary>
[Authorize]
public class TicketHub : Hub
{
    private readonly ApplicationDbContext         _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService                _audit;
    private readonly ILogger<TicketHub>           _logger;

    public TicketHub(
        ApplicationDbContext         db,
        UserManager<ApplicationUser> userManager,
        IAuditService                audit,
        ILogger<TicketHub>           logger)
    {
        _db          = db;
        _userManager = userManager;
        _audit       = audit;
        _logger      = logger;
    }

    /// <summary>
    /// Liittää käyttäjän tiketin viestiryhmään. Adminit liittyvät
    /// lisäksi sisäisten muistiinpanojen ryhmään.
    /// </summary>
    public async Task JoinTicketGroup(string ticketId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}");

        if (Context.User!.IsInRole("Admin"))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}_Admin");
    }

    /// <summary>Poistuu tiketin viestiryhmistä (esim. kun siirrytään pois sivulta).</summary>
    public async Task LeaveTicketGroup(string ticketId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}_Admin");
    }

    /// <summary>
    /// Lisää uuden kommentin tikettiin ja lähettää sen muille käyttäjille
    /// reaaliaikaisesti. Tärkeät tarkistukset:
    ///  - Käyttäjän pääsy tikettiin (asiakas vain oma yritys)
    ///  - Tiketin status (suljettuun ei voi lisätä)
    ///  - Sisäisen viestin merkinnän voi tehdä vain admin
    /// </summary>
    public async Task SendMessage(int ticketId, string content, bool isInternal)
    {
        var user = await _userManager.GetUserAsync(Context.User!);
        if (user is null) return;

        // Pituusrajoitus: tyhjää ei tallenneta, yli 4000 merkin viestit hylätään.
        content = content?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(content) || content.Length > 4000) return;

        bool isAdmin = Context.User!.IsInRole("Admin");

        var ticket = await _db.Tickets.FindAsync(ticketId);
        if (ticket is null)
        {
            _logger.LogWarning("TicketHub: user {UserId} attempted to post to non-existent ticket {TicketId}", user.Id, ticketId);
            await _audit.LogAsync("TicketAccessDenied", "Ticket", ticketId.ToString(), "NotFound");
            return;
        }

        // IDOR-suoja: tarkistetaan että asiakas ei pääse käsiksi
        // toisen yrityksen tikettiin pelkän numeron avulla.
        if (!isAdmin && ticket.CustomerId != user.CustomerId)
        {
            _logger.LogWarning("TicketHub: user {UserId} denied access to ticket {TicketId}", user.Id, ticketId);
            await _audit.LogAsync("TicketAccessDenied", "Ticket", ticketId.ToString(), "IDOR attempt");
            return;
        }
        // Suljettuun tikettiin ei voi enää kirjoittaa.
        if (ticket.Status == TicketStatus.Closed) return;

        // Asiakas ei voi merkitä viestiä sisäiseksi — pakotetaan false.
        if (!isAdmin) isInternal = false;

        var comment = new TicketComment
        {
            TicketId   = ticketId,
            Content    = content,
            IsInternal = isInternal,
            AuthorId   = user.Id,
            CreatedAt  = DateTime.UtcNow
        };

        ticket.UpdatedAt = DateTime.UtcNow;

        // Jos asiakas vastaa "odottaa asiakasta" -tilassa olevaan tikettiin,
        // siirretään se takaisin "käsittelyssä"-tilaan.
        if (!isAdmin && ticket.Status == TicketStatus.WaitingCustomer)
            ticket.Status = TicketStatus.InProgress;

        _db.TicketComments.Add(comment);
        await _db.SaveChangesAsync();

        // Näytetään kirjoittajan koko nimi jos saatavilla, muuten sähköposti.
        string authorName = !string.IsNullOrWhiteSpace(user.FullName)
            ? user.FullName
            : user.Email ?? "—";

        // Selaimelle lähetettävä payload (anonymous object → JSON).
        var payload = new
        {
            commentId  = comment.Id,
            authorId   = user.Id,
            authorName,
            content    = comment.Content,
            timestamp  = comment.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
            isInternal,
            isAdmin
        };

        // Sisäiset muistiinpanot menevät vain admin-ryhmälle,
        // tavalliset kommentit kaikille tiketin osallistujille.
        string targetGroup = isInternal
            ? $"Ticket_{ticketId}_Admin"
            : $"Ticket_{ticketId}";

        await Clients.Group(targetGroup).SendAsync("ReceiveMessage", payload);
    }
}
