using Microsoft.AspNetCore.Mvc;
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
    public IReadOnlyList<string> TypeOptions { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> EnvOptions { get; private set; } = Array.Empty<string>();

    [BindProperty]
    public TargetForm Form { get; set; } = new();

    [BindProperty]
    public TargetForm UpdateForm { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? EditId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EnvFilter { get; set; }

    public string? ErrorMessage { get; private set; }
    public string? UpdateErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var all = await _apiClient.GetTargetsAsync(cancellationToken);

        TypeOptions = all.Select(t => t.Type).Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToList();
        EnvOptions = all.Select(t => t.Environment).Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(e => e).ToList();

        var filtered = all.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(Search))
            filtered = filtered.Where(t =>
                t.Id.Contains(Search, StringComparison.OrdinalIgnoreCase)
                || t.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)
                || (t.Host ?? "").Contains(Search, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(TypeFilter))
            filtered = filtered.Where(t => string.Equals(t.Type, TypeFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(EnvFilter))
            filtered = filtered.Where(t => string.Equals(t.Environment, EnvFilter, StringComparison.OrdinalIgnoreCase));

        Targets = filtered.ToList();

        if (!string.IsNullOrWhiteSpace(EditId))
        {
            var target = Targets.FirstOrDefault(item => item.Id.Equals(EditId, StringComparison.OrdinalIgnoreCase));
            if (target is not null)
            {
                UpdateForm = new TargetForm
                {
                    Id = target.Id,
                    Name = target.Name,
                    Host = target.Host,
                    Port = target.Port,
                    Type = target.Type,
                    Environment = target.Environment,
                    Criticality = target.Criticality,
                    Status = target.Status,
                    Labels = target.Labels is null
                        ? null
                        : string.Join(", ", target.Labels.Select(kvp => $"{kvp.Key}={kvp.Value}"))
                };
            }
        }
    }

    public async Task OnPostAsync(CancellationToken cancellationToken)
    {
        Targets = await _apiClient.GetTargetsAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(Form.Id) || string.IsNullOrWhiteSpace(Form.Name))
        {
            ErrorMessage = "Id и Name обязательны.";
            return;
        }

        var labels = ParseLabels(Form.Labels);
        var dto = new TargetUpsertDto(
            Form.Id.Trim(),
            Form.Name.Trim(),
            string.IsNullOrWhiteSpace(Form.Host) ? null : Form.Host.Trim(),
            Form.Port,
            labels,
            Form.Type?.Trim() ?? string.Empty,
            Form.Environment?.Trim() ?? string.Empty,
            Form.Criticality?.Trim() ?? string.Empty,
            Form.Status?.Trim() ?? string.Empty
        );

        var created = await _apiClient.CreateTargetAsync(dto, cancellationToken);
        if (created is null)
        {
            ErrorMessage = "Не удалось создать Target. Проверьте права и данные.";
            return;
        }

        Targets = await _apiClient.GetTargetsAsync(cancellationToken);
        Form = new TargetForm();
    }

    public async Task OnPostUpdateAsync(CancellationToken cancellationToken)
    {
        Targets = await _apiClient.GetTargetsAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(UpdateForm.Id) || string.IsNullOrWhiteSpace(UpdateForm.Name))
        {
            UpdateErrorMessage = "Id и Name обязательны для обновления.";
            return;
        }

        var labels = ParseLabels(UpdateForm.Labels);
        var dto = new TargetUpsertDto(
            UpdateForm.Id.Trim(),
            UpdateForm.Name.Trim(),
            string.IsNullOrWhiteSpace(UpdateForm.Host) ? null : UpdateForm.Host.Trim(),
            UpdateForm.Port,
            labels,
            UpdateForm.Type?.Trim() ?? string.Empty,
            UpdateForm.Environment?.Trim() ?? string.Empty,
            UpdateForm.Criticality?.Trim() ?? string.Empty,
            UpdateForm.Status?.Trim() ?? string.Empty
        );

        var updated = await _apiClient.UpdateTargetAsync(dto, cancellationToken);
        if (updated is null)
        {
            UpdateErrorMessage = "Не удалось обновить Target. Проверьте права и данные.";
            return;
        }

        Targets = await _apiClient.GetTargetsAsync(cancellationToken);
        UpdateForm = new TargetForm();
    }

    private static Dictionary<string, string>? ParseLabels(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pairs = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
            {
                map[parts[0]] = parts[1];
            }
        }

        return map.Count == 0 ? null : map;
    }
}

public sealed class TargetForm
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Type { get; set; } = "Remote Desktop";
    public string? Environment { get; set; } = "prod";
    public string? Criticality { get; set; } = "critical";
    public string? Status { get; set; } = "Используется";
    public string? Labels { get; set; }
}
