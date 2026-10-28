using CustomerSupportAI.Models;

namespace CustomerSupportAI.ViewModels;

public static class DisplayText
{
    public static string Role(string? role) => role switch
    {
        UserRoles.Admin => "Quản trị viên",
        UserRoles.Staff => "Nhân viên hỗ trợ",
        UserRoles.Customer => "Khách hàng",
        _ => "Người dùng"
    };

    public static string TicketStatus(string status) => status switch
    {
        "Open" => "Mới tiếp nhận",
        "In progress" => "Đang xử lý",
        "Resolved" => "Đã giải quyết",
        "Closed" => "Đã đóng",
        _ => status
    };

    public static string TicketCategory(string category) => category switch
    {
        "Product support" => "Hỗ trợ sản phẩm",
        "Billing" => "Hóa đơn và thanh toán",
        "Warranty & repair" => "Bảo hành và sửa chữa",
        "Account access" => "Truy cập tài khoản",
        "Other" => "Khác",
        _ => category
    };
}