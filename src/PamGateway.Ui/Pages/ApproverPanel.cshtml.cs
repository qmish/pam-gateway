using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PamGateway.Ui.Pages;

public sealed class ApproverPanelModel : PageModel
{
    private readonly ApiClient _apiClient;

    public ApproverPanelModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IReadOnlyList<AccessRequestDto> PendingRequests { get; private set; } = Array.Empty<AccessRequestDto>();
    public IReadOnlyList<ApprovalDto> RecentApprovals { get; private set; } = Array.Empty<ApprovalDto>();
    public IReadOnlyList<TargetDto> Targets { get; private set; } = Array.Empty<TargetDto>();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TargetId { get; set; }

    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadDataAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostApproveAsync(string id, CancellationToken cancellationToken)
    {
        var result = await _apiClient.ApproveAccessRequestAsync(id, cancellationToken);
        if (result is null)
        {
            ErrorMessage = $"Failed to approve request {id}.";
            await LoadDataAsync(cancellationToken);
            return Page();
        }

        SuccessMessage = $"Request {id} approved.";
        await LoadDataAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostDenyAsync(string id, CancellationToken cancellationToken)
    {
        var result = await _apiClient.DenyAccessRequestAsync(id, cancellationToken);
        if (result is null)
        {
            ErrorMessage = $"Failed to deny request {id}.";
            await LoadDataAsync(cancellationToken);
            return Page();
        }

        SuccessMessage = $"Request {id} denied.";
        await LoadDataAsync(cancellationToken);
        return Page();
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        Targets = await _apiClient.GetTargetsAsync(cancellationToken);

        var allRequests = await _apiClient.GetAccessRequestsAsync(cancellationToken);
        if (allRequests is null)
        {
            ErrorMessage = "Failed to load access requests from API.";
            PendingRequests = Array.Empty<AccessRequestDto>();
        }
        else
        {
            var pending = allRequests
                .Where(r => r.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(Search))
                pending = pending.Where(r =>
                    r.Id.Contains(Search, StringComparison.OrdinalIgnoreCase)
                    || r.TargetId.Contains(Search, StringComparison.OrdinalIgnoreCase)
                    || r.RequestedBy.Contains(Search, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(TargetId))
                pending = pending.Where(r =>
                    r.TargetId.Equals(TargetId, StringComparison.OrdinalIgnoreCase));

            PendingRequests = pending
                .OrderBy(r => r.CreatedAt)
                .ToList();
        }

        var approvals = await _apiClient.GetApprovalsAsync(cancellationToken);
        RecentApprovals = (approvals ?? Array.Empty<ApprovalDto>())
            .OrderByDescending(a => a.ApprovedAt)
            .Take(20)
            .ToList();
    }
}
