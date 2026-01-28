using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PamGateway.Ui.Pages;

public sealed class RolesModel : PageModel
{
    private readonly ApiClient _apiClient;

    public RolesModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IReadOnlyList<RoleDto> Roles { get; private set; } = Array.Empty<RoleDto>();

    [BindProperty]
    public RoleForm Form { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Roles = await _apiClient.GetRolesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Form.Name))
        {
            ErrorMessage = "Название роли обязательно.";
            await OnGetAsync(cancellationToken);
            return Page();
        }

        var created = await _apiClient.CreateRoleAsync(
            new RoleCreateDto(Form.Name.Trim(), Form.Description?.Trim() ?? string.Empty),
            cancellationToken);

        if (created is null)
        {
            ErrorMessage = "Не удалось создать роль. Проверьте доступ к API.";
            await OnGetAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }
}

public sealed class RoleForm
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
