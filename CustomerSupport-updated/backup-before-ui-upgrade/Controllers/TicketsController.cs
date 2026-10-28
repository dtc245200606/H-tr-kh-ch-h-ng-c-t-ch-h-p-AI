using System.Security.Claims;
using CustomerSupportAI.Data;
using CustomerSupportAI.Models;
using CustomerSupportAI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportAI.Controllers;

[Authorize]
public sealed class TicketsController(AppDbContext db) : Controller
{
    private static readonly string[] Categories =
    [
        "Product support",
        "Billing",
        "Warranty & repair",
        "Account access",
        "Other"
    ];

    private static readonly string[] Statuses = ["Open", "In progress", "Resolved", "Closed"];

    public async Task<IActionResult> Index(string? search, string? status)
    {
        var tickets = VisibleTickets();
        var totalCount = await tickets.CountAsync();
        var openCount = await tickets.CountAsync(ticket => ticket.Status == "Open" || ticket.Status == "In progress");
        var resolvedCount = await tickets.CountAsync(ticket => ticket.Status == "Resolved");

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            tickets = tickets.Where(ticket =>
                ticket.Subject.Contains(term) || ticket.TicketNumber.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status) && Statuses.Contains(status, StringComparer.Ordinal))
        {
            tickets = tickets.Where(ticket => ticket.Status == status);
        }

        var items = await tickets
            .Include(ticket => ticket.Customer)
            .OrderByDescending(ticket => ticket.Id)
            .Take(100)
            .ToListAsync();

        return View(new TicketIndexViewModel
        {
            Tickets = items,
            Search = search?.Trim() ?? string.Empty,
            Status = status ?? string.Empty,
            TotalCount = totalCount,
            OpenCount = openCount,
            ResolvedCount = resolvedCount,
            IsStaff = IsStaff()
        });
    }

    [Authorize(Roles = UserRoles.Customer)]
    [HttpGet]
    public IActionResult Create() => View(new TicketCreateViewModel());

    [Authorize(Roles = UserRoles.Customer)]
    [HttpPost]
    public async Task<IActionResult> Create(TicketCreateViewModel model)
    {
        if (!Categories.Contains(model.Category, StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(model.Category), "Vui lòng chọn danh mục hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var customerId = CurrentUserId();
        var ticket = new SupportTicket
        {
            TicketNumber = $"CS-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            Subject = model.Subject.Trim(),
            Category = model.Category,
            Status = "Open",
            CustomerId = customerId
        };
        ticket.Messages.Add(new TicketMessage
        {
            AuthorId = customerId,
            Body = model.FirstMessage.Trim()
        });

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Đã tạo yêu cầu {ticket.TicketNumber}.";
        return RedirectToAction(nameof(Details), new { id = ticket.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var ticket = await VisibleTickets()
            .Include(item => item.Customer)
            .Include(item => item.Messages.OrderBy(message => message.Id))
                .ThenInclude(message => message.Author)
            .SingleOrDefaultAsync(item => item.Id == id);

        return ticket is null
            ? NotFound()
            : View(new TicketDetailsViewModel { Ticket = ticket, IsStaff = IsStaff() });
    }

    [HttpPost]
    public async Task<IActionResult> Reply(TicketReplyViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vui lòng nhập phản hồi không quá 4.000 ký tự.";
            return RedirectToAction(nameof(Details), new { id = model.TicketId });
        }

        var ticket = await VisibleTickets().SingleOrDefaultAsync(item => item.Id == model.TicketId);
        if (ticket is null)
        {
            return NotFound();
        }

        if (ticket.Status == "Closed")
        {
            TempData["Error"] = "Yêu cầu đã đóng nên không thể nhận thêm phản hồi.";
            return RedirectToAction(nameof(Details), new { id = ticket.Id });
        }

        db.TicketMessages.Add(new TicketMessage
        {
            TicketId = ticket.Id,
            AuthorId = CurrentUserId(),
            Body = model.Body.Trim()
        });
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        if (IsStaff() && ticket.Status == "Open")
        {
            ticket.Status = "In progress";
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "Đã gửi phản hồi.";
        return RedirectToAction(nameof(Details), new { id = ticket.Id });
    }

    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        if (!Statuses.Contains(status, StringComparer.Ordinal))
        {
            return BadRequest();
        }

        var ticket = await db.Tickets.SingleOrDefaultAsync(item => item.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        ticket.Status = status;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật trạng thái yêu cầu.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private IQueryable<SupportTicket> VisibleTickets()
    {
        var tickets = db.Tickets.AsQueryable();
        return IsStaff()
            ? tickets
            : tickets.Where(ticket => ticket.CustomerId == CurrentUserId());
    }

    private bool IsStaff() => User.IsInRole(UserRoles.Admin) || User.IsInRole(UserRoles.Staff);

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}