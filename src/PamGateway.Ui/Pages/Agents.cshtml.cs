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

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Agents = await _apiClient.GetAgentsAsync(cancellationToken);
    }
}
