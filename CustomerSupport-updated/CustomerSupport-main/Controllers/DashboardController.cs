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
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<IActionResult> Index(int range = 30)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var staff = User.IsInRole(UserRoles.Admin) || User.IsInRole(UserRoles.Staff);
        var query = db.Tickets.AsNoTracking().Where(t => staff || t.CustomerId == id);
        range = range is 7 or 90 ? range : 30;

        // SQLite cannot filter or group by DateTimeOffset, so the time series is built in memory.
        var rows = await query.Select(t => new { t.CreatedAt, t.Status }).ToListAsync();
        var today = DateTimeOffset.UtcNow.ToOffset(VietnamOffset).Date;
        var bucketDays = range == 90 ? 7 : 1;
        var bucketCount = range == 90 ? 13 : range;
        var from = today.AddDays(-(bucketCount * bucketDays - 1));
        var span = today.AddDays(1) - from;
        var createdDays = rows.Select(r => r.CreatedAt.ToOffset(VietnamOffset).Date).ToList();
        var trend = Enumerable.Range(0, bucketCount).Select(i =>
        {
            var start = from.AddDays(i * bucketDays);
            var end = start.AddDays(bucketDays);
            return new TrendPoint(start, createdDays.Count(day => day >= start && day < end));
        }).ToList();

        var openQueue = query.Where(t => t.Status == "Open" || t.Status == "In progress");
        return View(new DashboardViewModel {
            IsStaff = staff, Total = rows.Count,
            Open = rows.Count(t => t.Status == "Open" || t.Status == "In progress"),
            Resolved = rows.Count(t => t.Status == "Resolved" || t.Status == "Closed"),
            Conversations = await db.AssistantTurns.CountAsync(t => t.UserId == id),
            Recent = await query.Include(t => t.Customer).OrderByDescending(t => t.Id).Take(5).ToListAsync(),
            Queue = staff ? await openQueue.Include(t => t.Customer).OrderBy(t => t.Id).Take(5).ToListAsync() : [],
            Categories = await query.GroupBy(t => t.Category).Select(g => new { Name = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Name, g => g.Count),
            StatusCounts = rows.GroupBy(t => t.Status).ToDictionary(g => g.Key, g => g.Count()),
            Range = range, BucketDays = bucketDays, Trend = trend,
            NewInRange = createdDays.Count(day => day >= from),
            NewPrevious = createdDays.Count(day => day >= from - span && day < from),
            Activity = await db.TicketMessages.AsNoTracking().Where(m => staff || m.Ticket.CustomerId == id)
                .Include(m => m.Author).Include(m => m.Ticket).OrderByDescending(m => m.Id).Take(5).ToListAsync()
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
