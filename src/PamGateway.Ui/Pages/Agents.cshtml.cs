using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PamGateway.Ui.Pages;

public sealed class AgentsModel : PageModel
{
    private readonly ApiClient _apiClient;

    public AgentsModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IReadOnlyList<AgentDto> Agents { get; private set; } = Array.Empty<AgentDto>();
    public IReadOnlyList<SessionDto> Sessions { get; private set; } = Array.Empty<SessionDto>();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    public int OnlineCount => Agents.Count(a => a.Status.Equals("Online", StringComparison.OrdinalIgnoreCase));
    public int OfflineCount => Agents.Count(a => !a.Status.Equals("Online", StringComparison.OrdinalIgnoreCase));

    public int GetActiveSessions(string agentId) =>
        Sessions.Count(s => s.Status.Equals("Active", StringComparison.OrdinalIgnoreCase));

    public string GetUptime(AgentDto agent)
    {
        var diff = DateTimeOffset.UtcNow - agent.LastSeenAt;
        if (diff.TotalDays >= 1)
            return $"{(int)diff.TotalDays}d {diff.Hours}h";
        if (diff.TotalHours >= 1)
            return $"{(int)diff.TotalHours}h {diff.Minutes}m";
        return $"{(int)diff.TotalMinutes}m";
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Agents = await _apiClient.GetAgentsAsync(cancellationToken);
        Sessions = await _apiClient.GetSessionsAsync(cancellationToken) ?? Array.Empty<SessionDto>();

        var filtered = Agents.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            filtered = filtered.Where(a =>
                a.Hostname.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                a.Id.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                a.Os.Contains(Search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(StatusFilter))
        {
            filtered = filtered.Where(a =>
                a.Status.Equals(StatusFilter, StringComparison.OrdinalIgnoreCase));
        }

        Agents = filtered.ToList();
    }
}
