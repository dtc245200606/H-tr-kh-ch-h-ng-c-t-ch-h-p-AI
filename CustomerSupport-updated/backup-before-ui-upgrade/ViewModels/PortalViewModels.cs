using CustomerSupportAI.Models;
namespace CustomerSupportAI.ViewModels;
public sealed class DashboardViewModel
{
    public int Total { get; set; }
    public int Open { get; set; }
    public int Resolved { get; set; }
    public int Conversations { get; set; }
    public bool IsStaff { get; set; }
    public List<SupportTicket> Recent { get; set; } = [];
    public Dictionary<string, int> Categories { get; set; } = [];
}
public sealed record KnowledgeViewModel(IReadOnlyList<KnowledgeArticle> Articles, string Search, string Category);
public sealed record AssistantPageViewModel(IReadOnlyList<AssistantTurn> History, bool IsConfigured);
public sealed record ActivityViewModel(IReadOnlyList<TicketMessage> Messages);
