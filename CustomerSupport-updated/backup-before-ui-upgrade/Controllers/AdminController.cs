using CustomerSupportAI.Data;
using CustomerSupportAI.Models;
using CustomerSupportAI.Services;
using CustomerSupportAI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportAI.Controllers;

[Authorize(Roles = UserRoles.Admin)]
public sealed class AdminController(AppDbContext db, UserAdministrationService admin) : Controller
{
    private const string Portal = "Admin";

    [HttpGet]
    public async Task<IActionResult> Users(string? search, string? role)
    {
        return View(await BuildUsersViewModelAsync(new CreateStaffViewModel(), search, role));
    }

    [HttpPost]
    public async Task<IActionResult> CreateStaff([Bind(Prefix = "NewStaff")] CreateStaffViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Users", await BuildUsersViewModelAsync(model, null, null));
        }

        var result = await admin.CreateAsync(User, model.DisplayName, model.Email, UserRoles.Staff, model.TemporaryPassword);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("NewStaff.Email", result.Error!);
            return View("Users", await BuildUsersViewModelAsync(model, null, null));
        }

        TempData["Success"] = $"Đã tạo tài khoản nhân viên cho {model.Email.Trim()}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public IActionResult Create() => View("UserCreate", NewCreateModel());

    [HttpPost]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        model.Portal = Portal;
        model.AllowedRoles = UserRoles.All.ToList();
        if (!ModelState.IsValid)
        {
            return View("UserCreate", model);
        }

        var result = await admin.CreateAsync(User, model.DisplayName, model.Email, model.Role, model.Password);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
            return View("UserCreate", model);
        }

        TempData["Success"] = $"Đã tạo tài khoản {model.Email.Trim()}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        return user is null
            ? NotFound()
            : View("UserDetails", await admin.BuildDetailsAsync(User, user, Portal));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        return user is null
            ? NotFound()
            : View("UserEdit", UserAdministrationService.ToEditModel(User, user, Portal));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, UserEditViewModel model)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        UserAdministrationService.Decorate(model, User, user, Portal);
        if (!ModelState.IsValid)
        {
            return View("UserEdit", model);
        }

        var result = await admin.UpdateAsync(
            User, user, model.DisplayName, model.Email, model.Role, model.IsActive, model.NewPassword);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
            return View("UserEdit", model);
        }

        TempData["Success"] = "Đã cập nhật tài khoản.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool active)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        var result = await admin.SetActiveAsync(User, user, active);
        TempData[result.Succeeded ? "Success" : "Error"] =
            result.Succeeded ? (active ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.") : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        var email = user.Email;
        var result = await admin.DeleteAsync(User, user);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = $"Đã xóa tài khoản {email}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> AuditLog()
    {
        var entries = await db.AuditLogs.AsNoTracking()
            .OrderByDescending(entry => entry.Id)
            .Take(200)
            .ToListAsync();
        return View(entries);
    }

    private UserCreateViewModel NewCreateModel() => new()
    {
        Portal = Portal,
        AllowedRoles = UserRoles.All.ToList(),
        Role = UserRoles.Staff
    };

    private async Task<AdminUsersViewModel> BuildUsersViewModelAsync(
        CreateStaffViewModel newStaff, string? search, string? role)
    {
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(role) && UserRoles.All.Contains(role))
        {
            query = query.Where(user => user.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user => user.DisplayName.Contains(term) || user.Email.Contains(term));
        }

        var users = await query.OrderBy(user => user.Id).ToListAsync();
        var ticketCounts = await db.Tickets
            .GroupBy(ticket => ticket.CustomerId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.UserId, item => item.Count);

        return new AdminUsersViewModel
        {
            Users = users.Select(user => new UserListItemViewModel
            {
                User = user,
                TicketCount = ticketCounts.GetValueOrDefault(user.Id)
            }).ToList(),
            NewStaff = newStaff,
            Search = search?.Trim() ?? string.Empty,
            Role = role ?? string.Empty
        };
    }
}
