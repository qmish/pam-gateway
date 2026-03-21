using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PamGateway.Ui.Pages;

public sealed class DashboardModel : PageModel
{
    private readonly ApiClient _apiClient;

    public DashboardModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public int TotalTargets { get; private set; }
    public int OnlineAgents { get; private set; }
    public int OfflineAgents { get; private set; }
    public int ActiveSessions { get; private set; }
    public int PendingRequests { get; private set; }
    public int ApprovedRequests { get; private set; }
    public int TotalRecordings { get; private set; }
    public int TotalPolicies { get; private set; }

    public IReadOnlyList<AccessRequestDto> RecentRequests { get; private set; } = [];
    public IReadOnlyList<SessionDto> RecentSessions { get; private set; } = [];
    public IReadOnlyList<AgentDto> Agents { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var targets = await _apiClient.GetTargetsAsync(cancellationToken);
        var agents = await _apiClient.GetAgentsAsync(cancellationToken);
        var sessions = await _apiClient.GetSessionsAsync(cancellationToken) ?? [];
        var requests = await _apiClient.GetAccessRequestsAsync(cancellationToken) ?? [];
        var recordings = await _apiClient.GetRecordingsAsync(cancellationToken) ?? [];
        var policies = await _apiClient.GetPoliciesAsync(cancellationToken);

        TotalTargets = targets.Count;
        Agents = agents;
        OnlineAgents = agents.Count(a => a.Status.Equals("Online", StringComparison.OrdinalIgnoreCase));
        OfflineAgents = agents.Count - OnlineAgents;
        ActiveSessions = sessions.Count(s => s.Status.Equals("Active", StringComparison.OrdinalIgnoreCase));
        PendingRequests = requests.Count(r => r.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase));
        ApprovedRequests = requests.Count(r => r.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase));
        TotalRecordings = recordings.Count;
        TotalPolicies = policies.Count;

        RecentRequests = requests
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToList();

        RecentSessions = sessions
            .OrderByDescending(s => s.StartedAt)
            .Take(5)
            .ToList();
    }
}
