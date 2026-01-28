using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PamGateway.Ui.Pages;

public sealed class ApprovalsModel : PageModel
{
    private readonly ApiClient _apiClient;

    public ApprovalsModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IReadOnlyList<ApprovalDto> Approvals { get; private set; } = Array.Empty<ApprovalDto>();
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var approvals = await _apiClient.GetApprovalsAsync(cancellationToken);
        Approvals = approvals ?? Array.Empty<ApprovalDto>();
        if (approvals is null)
        {
            ErrorMessage = "Не удалось получить список approvals. Проверьте доступ к API.";
        }
    }
}
