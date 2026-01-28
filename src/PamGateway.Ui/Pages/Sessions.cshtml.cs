using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PamGateway.Ui.Pages;

public sealed class SessionsModel : PageModel
{
    private readonly ApiClient _apiClient;

    public SessionsModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IReadOnlyList<SessionDto> Sessions { get; private set; } = Array.Empty<SessionDto>();
    public IReadOnlyList<string> StatusOptions { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> ProtocolOptions { get; private set; } = Array.Empty<string>();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Protocol { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var sessions = await _apiClient.GetSessionsAsync(cancellationToken);
        if (sessions is null)
        {
            ErrorMessage = "Не удалось получить список сессий (API вернул ошибку).";
            Sessions = Array.Empty<SessionDto>();
            StatusOptions = Array.Empty<string>();
            ProtocolOptions = Array.Empty<string>();
            return;
        }
        StatusOptions = sessions.Select(item => item.Status).Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList();
        ProtocolOptions = sessions.Select(item => item.Protocol).Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList();

        var filtered = sessions.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            filtered = filtered.Where(item =>
                Contains(item.Id, Search)
                || Contains(item.TargetId, Search)
                || Contains(item.RequestId, Search));
        }

        if (!string.IsNullOrWhiteSpace(Status))
        {
            filtered = filtered.Where(item => string.Equals(item.Status, Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(Protocol))
        {
            filtered = filtered.Where(item => string.Equals(item.Protocol, Protocol, StringComparison.OrdinalIgnoreCase));
        }

        Sessions = filtered.OrderByDescending(item => item.StartedAt).ToList();
        if (sessions.Count == 0)
        {
            ErrorMessage = "Сессии не найдены или доступ к API ограничен.";
        }
    }

    private static bool Contains(string value, string search)
        => value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
