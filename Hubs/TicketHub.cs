using HakaTech.Portal.Data;
using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Hubs;

[Authorize]
public class TicketHub : Hub
{
    private readonly ApplicationDbContext         _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public TicketHub(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db          = db;
        _userManager = userManager;
    }

    public async Task JoinTicketGroup(string ticketId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}");

        if (Context.User!.IsInRole("Admin"))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}_Admin");
    }

    public async Task LeaveTicketGroup(string ticketId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}_Admin");
    }

    public async Task SendMessage(int ticketId, string content, bool isInternal)
    {
        var user = await _userManager.GetUserAsync(Context.User!);
        if (user is null) return;

        content = content?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(content) || content.Length > 4000) return;

        bool isAdmin = Context.User!.IsInRole("Admin");

        var ticket = await _db.Tickets.FindAsync(ticketId);
        if (ticket is null) return;

        if (!isAdmin && ticket.CustomerId != user.CustomerId) return;
        if (ticket.Status == TicketStatus.Closed) return;

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

        if (!isAdmin && ticket.Status == TicketStatus.WaitingCustomer)
            ticket.Status = TicketStatus.InProgress;

        _db.TicketComments.Add(comment);
        await _db.SaveChangesAsync();

        string authorName = !string.IsNullOrWhiteSpace(user.FullName)
            ? user.FullName
            : user.Email ?? "—";

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

        // Sisäiset viestit vain admin-ryhmälle, julkiset kaikille
        string targetGroup = isInternal
            ? $"Ticket_{ticketId}_Admin"
            : $"Ticket_{ticketId}";

        await Clients.Group(targetGroup).SendAsync("ReceiveMessage", payload);
    }
}
