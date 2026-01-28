using Microsoft.AspNetCore.Mvc;
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
    public IReadOnlyList<string> StatusOptions { get; private set; } = Array.Empty<string>();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var approvals = await _apiClient.GetApprovalsAsync(cancellationToken);
        if (approvals is null)
        {
            ErrorMessage = "Не удалось получить список approvals. Проверьте доступ к API.";
            Approvals = Array.Empty<ApprovalDto>();
            return;
        }

        StatusOptions = approvals.Select(item => item.Status)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToList();

        var filtered = approvals.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            filtered = filtered.Where(item =>
                Contains(item.Id, Search)
                || Contains(item.RequestId, Search)
                || Contains(item.Approver, Search));
        }

        if (!string.IsNullOrWhiteSpace(Status))
        {
            filtered = filtered.Where(item => string.Equals(item.Status, Status, StringComparison.OrdinalIgnoreCase));
        }

        Approvals = filtered.OrderByDescending(item => item.ApprovedAt).ToList();
    }

    private static bool Contains(string value, string search)
        => value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
