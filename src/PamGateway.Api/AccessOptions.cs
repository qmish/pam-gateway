namespace PamGateway.Api;

public sealed class AccessOptions
{
    public Dictionary<string, Dictionary<string, string>> RoleLabelRules { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> RoleLabelExpressions { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<string>> RolePolicyIds { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Role hierarchy: child → parent. Child inherits all policies from parent.
    /// Example: "System_Admin_Windows" → "App_Support" means Windows admins
    /// also get all App_Support policies.
    /// </summary>
    public Dictionary<string, string> RoleHierarchy { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);
}
