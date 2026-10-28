using System.ComponentModel.DataAnnotations;
using CustomerSupportAI.Models;

namespace CustomerSupportAI.ViewModels;

public sealed class TicketIndexViewModel
{
    public IReadOnlyList<SupportTicket> Tickets { get; init; } = [];
    public string Search { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int TotalCount { get; init; }
    public int OpenCount { get; init; }
    public int ResolvedCount { get; init; }
    public bool IsStaff { get; init; }
}

public sealed class TicketCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề yêu cầu."), MaxLength(120, ErrorMessage = "Tiêu đề không được vượt quá 120 ký tự.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung yêu cầu."), StringLength(4000, MinimumLength = 10, ErrorMessage = "Nội dung cần có từ {2} đến {1} ký tự.")]
    public string FirstMessage { get; set; } = string.Empty;
}

public sealed class TicketReplyViewModel
{
    [Required]
    public int TicketId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nội dung phản hồi."), StringLength(4000, MinimumLength = 1, ErrorMessage = "Phản hồi không được vượt quá {1} ký tự.")]
    public string Body { get; set; } = string.Empty;
}

public sealed class TicketDetailsViewModel
{
    public required SupportTicket Ticket { get; init; }
    public bool IsStaff { get; init; }
}

public sealed class UserListItemViewModel
{
    public required SupportUser User { get; init; }
    public int TicketCount { get; init; }
}

public sealed class AdminUsersViewModel
{
    public IReadOnlyList<UserListItemViewModel> Users { get; init; } = [];
    public CreateStaffViewModel NewStaff { get; init; } = new();
    public string Search { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}