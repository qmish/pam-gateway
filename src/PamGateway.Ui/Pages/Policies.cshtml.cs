using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PamGateway.Ui.Pages;

public sealed class PoliciesModel : PageModel
{
    private readonly ApiClient _apiClient;

    public PoliciesModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IReadOnlyList<PolicyDto> Policies { get; private set; } = Array.Empty<PolicyDto>();

    [BindProperty]
    public PolicyForm Form { get; set; } = new();

    [BindProperty]
    public PolicyForm UpdateForm { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? EditId { get; set; }

    public string? ErrorMessage { get; private set; }
    public string? UpdateErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Policies = await _apiClient.GetPoliciesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(EditId))
        {
            var policy = Policies.FirstOrDefault(item => item.Id.Equals(EditId, StringComparison.OrdinalIgnoreCase));
            if (policy is not null)
            {
                UpdateForm = new PolicyForm
                {
                    Id = policy.Id,
                    Name = policy.Name,
                    TargetType = policy.TargetType,
                    AllowedProtocols = policy.AllowedProtocols,
                    Effect = policy.Effect,
                    TargetLabelSelector = policy.TargetLabelSelector is null
                        ? null
                        : string.Join(", ", policy.TargetLabelSelector.Select(kvp => $"{kvp.Key}={kvp.Value}"))
                };
            }
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Form.Name)
            || string.IsNullOrWhiteSpace(Form.TargetType)
            || string.IsNullOrWhiteSpace(Form.AllowedProtocols)
            || string.IsNullOrWhiteSpace(Form.Effect))
        {
            ErrorMessage = "Заполните обязательные поля.";
            await OnGetAsync(cancellationToken);
            return Page();
        }

        var selector = ParseSelector(Form.TargetLabelSelector);
        var policy = new PolicyCreateDto(
            Form.Name.Trim(),
            Form.TargetType.Trim(),
            Form.AllowedProtocols.Trim(),
            Form.Effect.Trim(),
            selector);

        var created = await _apiClient.CreatePolicyAsync(policy, cancellationToken);
        if (created is null)
        {
            ErrorMessage = "Не удалось создать политику. Проверьте доступ к API.";
            await OnGetAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UpdateForm.Id)
            || string.IsNullOrWhiteSpace(UpdateForm.Name)
            || string.IsNullOrWhiteSpace(UpdateForm.TargetType)
            || string.IsNullOrWhiteSpace(UpdateForm.AllowedProtocols)
            || string.IsNullOrWhiteSpace(UpdateForm.Effect))
        {
            UpdateErrorMessage = "Заполните обязательные поля.";
            await OnGetAsync(cancellationToken);
            return Page();
        }

        var selector = ParseSelector(UpdateForm.TargetLabelSelector);
        var policy = new PolicyUpsertDto(
            UpdateForm.Id.Trim(),
            UpdateForm.Name.Trim(),
            UpdateForm.TargetType.Trim(),
            UpdateForm.AllowedProtocols.Trim(),
            UpdateForm.Effect.Trim(),
            selector);

        var updated = await _apiClient.UpdatePolicyAsync(policy, cancellationToken);
        if (updated is null)
        {
            UpdateErrorMessage = "Не удалось обновить политику. Проверьте доступ к API.";
            await OnGetAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    private static Dictionary<string, string>? ParseSelector(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pairs = input.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0];
            var value = parts[1];
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result[key] = value;
        }

        return result.Count == 0 ? null : result;
    }
}

public sealed class PolicyForm
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string AllowedProtocols { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty;
    public string? TargetLabelSelector { get; set; }
}
