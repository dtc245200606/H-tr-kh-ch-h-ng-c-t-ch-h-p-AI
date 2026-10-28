using System.Security.Claims;
using CustomerSupportAI.Data;
using CustomerSupportAI.Models;
using CustomerSupportAI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace CustomerSupportAI.Controllers;
[Authorize]
public sealed class DashboardController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var staff = User.IsInRole(UserRoles.Admin) || User.IsInRole(UserRoles.Staff);
        var query = db.Tickets.AsNoTracking().Where(t => staff || t.CustomerId == id);
        return View(new DashboardViewModel {
            IsStaff = staff, Total = await query.CountAsync(),
            Open = await query.CountAsync(t => t.Status == "Open" || t.Status == "In progress"),
            Resolved = await query.CountAsync(t => t.Status == "Resolved" || t.Status == "Closed"),
            Conversations = await db.AssistantTurns.CountAsync(t => t.UserId == id),
            Recent = await query.Include(t => t.Customer).OrderByDescending(t => t.Id).Take(5).ToListAsync(),
            Categories = await query.GroupBy(t => t.Category).Select(g => new { Name = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Name, g => g.Count)
        });
    }
    public async Task<IActionResult> Activity()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var staff = User.IsInRole(UserRoles.Admin) || User.IsInRole(UserRoles.Staff);
        var messages = await db.TicketMessages.AsNoTracking().Where(m => staff || m.Ticket.CustomerId == id)
            .Include(m => m.Author).Include(m => m.Ticket).OrderByDescending(m => m.Id).Take(50).ToListAsync();
        return View(new ActivityViewModel(messages));
    }
}
