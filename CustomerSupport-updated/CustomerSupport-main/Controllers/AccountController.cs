using System.Security.Claims;
using CustomerSupportAI.Data;
using CustomerSupportAI.Models;
using CustomerSupportAI.Services;
using CustomerSupportAI.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportAI.Controllers;

public sealed class AccountController(
    AppDbContext db,
    IPasswordHasher<SupportUser> passwordHasher,
    LoginThrottle throttle) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    /// <summary>
    /// Single sign-in screen for every role: the account found by email or phone number
    /// decides whether the user lands in the customer or the staff workspace.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToUpperInvariant();
        var phone = PhoneNumber.Normalize(model.Email);
        var throttleKey = LoginThrottle.Key(HttpContext, phone ?? normalizedEmail);
        if (throttle.IsBlocked(throttleKey))
        {
            ModelState.AddModelError(string.Empty, "Bạn đã đăng nhập sai quá nhiều lần. Vui lòng thử lại sau 15 phút.");
            return View(model);
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail)
            ?? (phone is null ? null : await db.Users.FirstOrDefaultAsync(item => item.PhoneNumber == phone));
        var verificationResult = user is null
            ? PasswordVerificationResult.Failed
            : passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
        if (user is null || verificationResult == PasswordVerificationResult.Failed)
        {
            throttle.RecordFailure(throttleKey);
            ModelState.AddModelError(string.Empty, "Email/số điện thoại hoặc mật khẩu không chính xác.");
            return View(model);
        }

        throttle.Reset(throttleKey);
        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
            return View(model);
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, model.Password);
            await db.SaveChangesAsync();
        }

        await SignInAsync(user, model.RememberMe);
        if (Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl!);
        }

        // Staff and admins start from the ticket queue, customers from their own overview.
        return user.Role is UserRoles.Admin or UserRoles.Staff
            ? RedirectToAction("Index", "Tickets")
            : RedirectToAction("Index", "Dashboard");
    }

    /// <summary>Old staff portal address; everyone now signs in on the shared screen.</summary>
    [HttpGet]
    public IActionResult StaffLogin(string? returnUrl = null) => RedirectToAction(nameof(Login), new { returnUrl });

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Tickets");
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        if (await db.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail))
        {
            ModelState.AddModelError(nameof(model.Email), "Email này đã được đăng ký.");
            return View(model);
        }

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
        {
            phone = PhoneNumber.Normalize(model.PhoneNumber);
            if (phone is null)
            {
                ModelState.AddModelError(nameof(model.PhoneNumber), "Số điện thoại không hợp lệ (ví dụ 0912345678).");
                return View(model);
            }
            if (await db.Users.AnyAsync(user => user.PhoneNumber == phone))
            {
                ModelState.AddModelError(nameof(model.PhoneNumber), "Số điện thoại này đã được đăng ký.");
                return View(model);
            }
        }

        var user = new SupportUser
        {
            DisplayName = model.DisplayName.Trim(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            PhoneNumber = phone,
            Role = UserRoles.Customer
        };
        user.PasswordHash = passwordHasher.HashPassword(user, model.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await SignInAsync(user, isPersistent: false);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost, Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return View(await db.Users.AsNoTracking().SingleAsync(u => u.Id == id));
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.SingleAsync(item => item.Id == userId);
        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword)
            == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không chính xác.");
            return View(model);
        }

        user.PasswordHash = passwordHasher.HashPassword(user, model.NewPassword);
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật mật khẩu.";
        return RedirectToAction("Index", "Tickets");
    }

    private Task SignInAsync(SupportUser user, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            AllowRefresh = true
        };

        return HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);
    }
}
