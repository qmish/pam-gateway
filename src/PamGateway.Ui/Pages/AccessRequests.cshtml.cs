using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PamGateway.Ui.Pages;

public sealed class AccessRequestsModel : PageModel
{
    private readonly ApiClient _apiClient;

    public AccessRequestsModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IReadOnlyList<AccessRequestDto> Requests { get; private set; } = Array.Empty<AccessRequestDto>();
    public IReadOnlyList<TargetDto> Targets { get; private set; } = Array.Empty<TargetDto>();

    [BindProperty]
    public AccessRequestForm Form { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var requests = await _apiClient.GetAccessRequestsAsync(cancellationToken);
        if (requests is null)
        {
            ErrorMessage = "Не удалось получить список заявок (API вернул ошибку).";
            Requests = Array.Empty<AccessRequestDto>();
        }
        else
        {
            Requests = requests;
        }
        Targets = await _apiClient.GetTargetsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Form.TargetId) || Form.DurationMinutes <= 0 || string.IsNullOrWhiteSpace(Form.Reason))
        {
            ErrorMessage = "Заполните Target, Duration и Reason.";
            await OnGetAsync(cancellationToken);
            return Page();
        }

        var request = new AccessRequestCreateDto(Form.TargetId, Form.DurationMinutes, Form.Reason.Trim());
        var created = await _apiClient.CreateAccessRequestAsync(request, cancellationToken);
        if (created is null)
        {
            ErrorMessage = "Не удалось создать заявку. Проверьте доступ к API.";
            await OnGetAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostApproveAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToPage();
        }

        var updated = await _apiClient.ApproveAccessRequestAsync(id, cancellationToken);
        if (updated is null)
        {
            ErrorMessage = "Не удалось согласовать заявку.";
            await OnGetAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDenyAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToPage();
        }

        var updated = await _apiClient.DenyAccessRequestAsync(id, cancellationToken);
        if (updated is null)
        {
            ErrorMessage = "Не удалось отклонить заявку.";
            await OnGetAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }
}

public sealed class AccessRequestForm
{
    public string TargetId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 60;
    public string Reason { get; set; } = string.Empty;
}
