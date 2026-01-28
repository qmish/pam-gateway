using System.Security.Claims;
using PamGateway.Core;
using Microsoft.Extensions.Options;

namespace PamGateway.Api;

public sealed class AccessPolicyEvaluator
{
    private readonly AccessOptions _options;
    private readonly IPolicyStore _policies;

    public AccessPolicyEvaluator(IOptions<AccessOptions> options, IPolicyStore policies)
    {
        _options = options.Value;
        _policies = policies;
    }

    public bool IsRequestAllowed(ClaimsPrincipal user, TargetSystem target, out string reason)
        => Evaluate(user, target, null, out reason);

    public bool IsSessionAllowed(ClaimsPrincipal user, TargetSystem target, string protocol, out string reason)
        => Evaluate(user, target, protocol, out reason);

    private bool Evaluate(ClaimsPrincipal user, TargetSystem target, string? protocol, out string reason)
    {
        reason = "Access policy denied.";

        if (user.Identity?.IsAuthenticated != true)
        {
            return true;
        }

        if (_options.RolePolicyIds.Count == 0)
        {
            return true;
        }

        var policies = GetPoliciesForUser(user);
        if (policies.Count == 0)
        {
            reason = "No matching policies for user roles.";
            return false;
        }

        var denyMatch = false;
        var allowMatch = false;
        foreach (var policy in policies)
        {
            if (!MatchesTarget(policy, target))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(protocol) && !AllowsProtocol(policy, protocol))
            {
                continue;
            }

            if (IsDeny(policy))
            {
                denyMatch = true;
                break;
            }

            allowMatch = true;
        }

        if (denyMatch)
        {
            reason = "Access denied by policy.";
            return false;
        }

        if (allowMatch)
        {
            return true;
        }

        reason = "No policies matched target.";
        return false;
    }

    private IReadOnlyList<Policy> GetPoliciesForUser(ClaimsPrincipal user)
    {
        var result = new List<Policy>();
        foreach (var (role, policyIds) in _options.RolePolicyIds)
        {
            if (!user.IsInRole(role))
            {
                continue;
            }

            foreach (var policyId in policyIds)
            {
                var policy = _policies.GetById(policyId);
                if (policy is not null)
                {
                    result.Add(policy);
                }
            }
        }

        return result;
    }

    private static bool MatchesTarget(Policy policy, TargetSystem target)
    {
        if (!string.IsNullOrWhiteSpace(policy.TargetType)
            && !string.Equals(policy.TargetType, "*", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(policy.TargetType, target.Type, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (policy.TargetLabelSelector is null || policy.TargetLabelSelector.Count == 0)
        {
            return true;
        }

        if (target.Labels is null || target.Labels.Count == 0)
        {
            return false;
        }

        return policy.TargetLabelSelector.All(pair =>
            target.Labels.TryGetValue(pair.Key, out var value)
            && string.Equals(value, pair.Value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool AllowsProtocol(Policy policy, string protocol)
    {
        if (string.IsNullOrWhiteSpace(policy.AllowedProtocols))
        {
            return true;
        }

        var entries = policy.AllowedProtocols
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var entry in entries)
        {
            if (string.Equals(entry, "*", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(entry, protocol, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDeny(Policy policy)
        => string.Equals(policy.Effect, "deny", StringComparison.OrdinalIgnoreCase);
}
