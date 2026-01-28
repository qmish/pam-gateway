using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PamGateway.Api;

public sealed class AuthRoleMappingOptions
{
    public bool EnableKeycloakMapping { get; set; } = true;
    public string RealmAccessClaim { get; set; } = "realm_access";
    public string ResourceAccessClaim { get; set; } = "resource_access";
    public string ResourceClientId { get; set; } = "pam-gateway";
    public string RoleClaimType { get; set; } = ClaimTypes.Role;
}

public sealed class KeycloakRoleClaimsTransformation : IClaimsTransformation
{
    private readonly AuthRoleMappingOptions _options;

    public KeycloakRoleClaimsTransformation(IOptions<AuthRoleMappingOptions> options)
    {
        _options = options.Value;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (!_options.EnableKeycloakMapping)
        {
            return Task.FromResult(principal);
        }

        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var existing = new HashSet<string>(
            identity.FindAll(_options.RoleClaimType).Select(claim => claim.Value),
            StringComparer.OrdinalIgnoreCase);

        AddRealmRoles(identity, existing);
        AddResourceRoles(identity, existing);

        return Task.FromResult(principal);
    }

    private void AddRealmRoles(ClaimsIdentity identity, HashSet<string> existing)
    {
        var realmClaim = identity.FindFirst(_options.RealmAccessClaim);
        if (realmClaim is null)
        {
            return;
        }

        if (!TryParseRoles(realmClaim.Value, out var roles))
        {
            return;
        }

        foreach (var role in roles)
        {
            if (existing.Add(role))
            {
                identity.AddClaim(new Claim(_options.RoleClaimType, role));
            }
        }
    }

    private void AddResourceRoles(ClaimsIdentity identity, HashSet<string> existing)
    {
        var resourceClaim = identity.FindFirst(_options.ResourceAccessClaim);
        if (resourceClaim is null)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(resourceClaim.Value);
            if (!doc.RootElement.TryGetProperty(_options.ResourceClientId, out var client))
            {
                return;
            }

            if (!client.TryGetProperty("roles", out var rolesElement) || rolesElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var roleElement in rolesElement.EnumerateArray())
            {
                if (roleElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var role = roleElement.GetString();
                if (string.IsNullOrWhiteSpace(role))
                {
                    continue;
                }

                if (existing.Add(role))
                {
                    identity.AddClaim(new Claim(_options.RoleClaimType, role));
                }
            }
        }
        catch
        {
            // Ignore malformed resource access data.
        }
    }

    private static bool TryParseRoles(string json, out List<string> roles)
    {
        roles = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("roles", out var rolesElement)
                || rolesElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var role in rolesElement.EnumerateArray())
            {
                if (role.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var value = role.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    roles.Add(value);
                }
            }

            return roles.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
