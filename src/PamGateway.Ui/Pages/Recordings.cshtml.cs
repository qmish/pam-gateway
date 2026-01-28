using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PamGateway.Ui.Pages;

public sealed class RecordingsModel : PageModel
{
    private readonly ApiClient _apiClient;
    private readonly IConfiguration _configuration;

    public RecordingsModel(ApiClient apiClient, IConfiguration configuration)
    {
        _apiClient = apiClient;
        _configuration = configuration;
    }

    public IReadOnlyList<RecordingDto> Recordings { get; private set; } = Array.Empty<RecordingDto>();
    public IReadOnlyList<string> StatusOptions { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> ModeOptions { get; private set; } = Array.Empty<string>();
    public string ApiPublicBaseUrl { get; private set; } = "/api";

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Mode { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ApiPublicBaseUrl = _configuration.GetValue<string>("Api:PublicBaseUrl") ?? "/api";
        var recordings = await _apiClient.GetRecordingsAsync(cancellationToken);
        if (recordings is null)
        {
            ErrorMessage = "Не удалось получить список записей (API вернул ошибку).";
            Recordings = Array.Empty<RecordingDto>();
            StatusOptions = Array.Empty<string>();
            ModeOptions = Array.Empty<string>();
            return;
        }
        StatusOptions = recordings.Select(item => item.Status).Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList();
        ModeOptions = recordings.Select(item => item.Mode).Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList();

        var filtered = recordings.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            filtered = filtered.Where(item =>
                Contains(item.Id, Search)
                || Contains(item.SessionId, Search));
        }

        if (!string.IsNullOrWhiteSpace(Status))
        {
            filtered = filtered.Where(item => string.Equals(item.Status, Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(Mode))
        {
            filtered = filtered.Where(item => string.Equals(item.Mode, Mode, StringComparison.OrdinalIgnoreCase));
        }

        Recordings = filtered.OrderByDescending(item => item.StartedAt).ToList();
        if (recordings.Count == 0)
        {
            ErrorMessage = "Записи сессий не найдены или доступ к API ограничен.";
        }
    }

    private static bool Contains(string value, string search)
        => value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
