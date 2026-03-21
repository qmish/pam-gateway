using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using PamGateway.Core;
using Microsoft.Extensions.Options;

namespace PamGateway.Api;

public sealed record PolicyDecisionAudit(
    string UserId,
    string TargetId,
    string? Protocol,
    bool Allowed,
    string Reason,
    IReadOnlyList<string> MatchedPolicyIds,
    string? DenyPolicyId);

public sealed class AccessPolicyEvaluator
{
    private readonly AccessOptions _options;
    private readonly IPolicyStore _policies;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public PolicyDecisionAudit? LastDecision { get; private set; }

    public AccessPolicyEvaluator(IOptions<AccessOptions> options, IPolicyStore policies, IMemoryCache cache)
    {
        _options = options.Value;
        _policies = policies;
        _cache = cache;
    }

    public bool IsRequestAllowed(ClaimsPrincipal user, TargetSystem target, out string reason)
        => Evaluate(user, target, null, out reason);

    public bool IsSessionAllowed(ClaimsPrincipal user, TargetSystem target, string protocol, out string reason)
        => Evaluate(user, target, protocol, out reason);

    public void InvalidateCache() => _cache.Remove("policy_eval_all_policies");

    private bool Evaluate(ClaimsPrincipal user, TargetSystem target, string? protocol, out string reason)
    {
        reason = "Access policy denied.";
        var matchedIds = new List<string>();
        string? denyPolicyId = null;

        if (user.Identity?.IsAuthenticated != true)
        {
            LastDecision = new PolicyDecisionAudit("anonymous", target.Id, protocol, true, "unauthenticated_bypass", matchedIds, null);
            return true;
        }

        var userId = user.FindFirst("sub")?.Value ?? user.Identity?.Name ?? "unknown";

        if (_options.RolePolicyIds.Count == 0)
        {
            LastDecision = new PolicyDecisionAudit(userId, target.Id, protocol, true, "no_role_policy_config", matchedIds, null);
            return true;
        }

        var policies = GetPoliciesForUser(user);
        if (policies.Count == 0)
        {
            reason = "No matching policies for user roles.";
            LastDecision = new PolicyDecisionAudit(userId, target.Id, protocol, false, reason, matchedIds, null);
            return false;
        }

        var denyMatch = false;
        var allowMatch = false;
        foreach (var policy in policies)
        {
            if (!MatchesTarget(policy, target))
                continue;
            if (!string.IsNullOrWhiteSpace(protocol) && !AllowsProtocol(policy, protocol))
                continue;

            matchedIds.Add(policy.Id);

            if (IsDeny(policy))
            {
                denyMatch = true;
                denyPolicyId = policy.Id;
                break;
            }

            allowMatch = true;
        }

        if (denyMatch)
        {
            reason = "Access denied by policy.";
            LastDecision = new PolicyDecisionAudit(userId, target.Id, protocol, false, reason, matchedIds, denyPolicyId);
            return false;
        }

        if (allowMatch)
        {
            reason = "";
            LastDecision = new PolicyDecisionAudit(userId, target.Id, protocol, true, "allowed", matchedIds, null);
            return true;
        }

        reason = "No policies matched target.";
        LastDecision = new PolicyDecisionAudit(userId, target.Id, protocol, false, reason, matchedIds, null);
        return false;
    }

    private IReadOnlyList<Policy> GetPoliciesForUser(ClaimsPrincipal user)
    {
        var allPolicies = _cache.GetOrCreate("policy_eval_all_policies", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return _policies.GetAll().ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);
        })!;

        var effectiveRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allKnownRoles = new HashSet<string>(_options.RolePolicyIds.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var key in _options.RoleHierarchy.Keys)
            allKnownRoles.Add(key);

        foreach (var role in allKnownRoles)
        {
            if (user.IsInRole(role))
            {
                effectiveRoles.Add(role);
                AddInheritedRoles(role, effectiveRoles);
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Policy>();
        foreach (var role in effectiveRoles)
        {
            if (!_options.RolePolicyIds.TryGetValue(role, out var policyIds))
                continue;
            foreach (var policyId in policyIds)
            {
                if (seen.Add(policyId) && allPolicies.TryGetValue(policyId, out var policy))
                    result.Add(policy);
            }
        }

        return result;
    }

    private void AddInheritedRoles(string role, HashSet<string> collected)
    {
        if (_options.RoleHierarchy.TryGetValue(role, out var parent)
            && !string.IsNullOrWhiteSpace(parent)
            && collected.Add(parent))
        {
            AddInheritedRoles(parent, collected);
        }
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
