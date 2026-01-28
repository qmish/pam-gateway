using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PamGateway.Ui.Pages;

public sealed class TargetsModel : PageModel
{
    private readonly ApiClient _apiClient;

    public TargetsModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IReadOnlyList<TargetDto> Targets { get; private set; } = Array.Empty<TargetDto>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Targets = await _apiClient.GetTargetsAsync(cancellationToken);
    }
}
