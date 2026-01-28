using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PamGateway.Core;
using Microsoft.Extensions.Options;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/targets")]
[Authorize]
public sealed class TargetsController : ControllerBase
{
    private readonly ITargetStore _targets;
    private readonly AccessOptions _accessOptions;
    private readonly IPolicyStore _policies;

    public TargetsController(ITargetStore targets, IPolicyStore policies, IOptions<AccessOptions> accessOptions)
    {
        _targets = targets;
        _policies = policies;
        _accessOptions = accessOptions.Value;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var targets = _targets.GetAll();
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(targets);
        }

        if (User.IsInRole("PAM_Administrator") || User.IsInRole("Security_Auditor"))
        {
            return Ok(targets);
        }

        var rules = GetUserLabelRules();
        var policyRules = GetUserPolicyRules();
        if (rules.Count == 0 && policyRules.Count == 0)
        {
            return Ok(Array.Empty<TargetSystem>());
        }

        var filtered = targets
            .Where(target => MatchesAnyRule(target, rules) || MatchesAnyPolicyRule(target, policyRules))
            .ToList();
        return Ok(filtered);
    }

    [HttpPost]
    [Authorize(Roles = "PAM_Administrator")]
    public IActionResult Create([FromBody] TargetUpsertDto dto)
    {
        var target = Map(dto);
        _targets.AddOrUpdate(target);
        return CreatedAtAction(nameof(GetById), new { id = target.Id }, target);
    }

    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var target = _targets.GetById(id);
        if (target is null)
        {
            return NotFound(new { message = "Target not found" });
        }

        return Ok(target);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "PAM_Administrator")]
    public IActionResult Update(string id, [FromBody] TargetUpsertDto dto)
    {
        if (!string.Equals(id, dto.Id, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Id mismatch" });
        }

        var target = Map(dto);
        _targets.AddOrUpdate(target);
        return Ok(target);
    }

    private IReadOnlyList<Dictionary<string, string>> GetUserLabelRules()
    {
        var rules = new List<Dictionary<string, string>>();
        foreach (var (role, labels) in _accessOptions.RoleLabelRules)
        {
            if (User.IsInRole(role))
            {
                rules.Add(labels);
            }
        }

        return rules;
    }

    private IReadOnlyList<PolicyRule> GetUserPolicyRules()
    {
        var rules = new List<PolicyRule>();
        foreach (var (role, policyIds) in _accessOptions.RolePolicyIds)
        {
            if (!User.IsInRole(role))
            {
                continue;
            }

            foreach (var policyId in policyIds)
            {
                var policy = _policies.GetById(policyId);
                if (policy?.TargetLabelSelector is null || policy.TargetLabelSelector.Count == 0)
                {
                    continue;
                }

                rules.Add(new PolicyRule(policy.TargetType, policy.TargetLabelSelector));
            }
        }

        return rules;
    }

    private static bool MatchesAnyRule(TargetSystem target, IReadOnlyList<Dictionary<string, string>> rules)
    {
        if (target.Labels is null || target.Labels.Count == 0)
        {
            return false;
        }

        foreach (var rule in rules)
        {
            if (rule.All(pair =>
                    target.Labels.TryGetValue(pair.Key, out var value)
                    && string.Equals(value, pair.Value, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAnyPolicyRule(TargetSystem target, IReadOnlyList<PolicyRule> rules)
    {
        if (target.Labels is null || target.Labels.Count == 0)
        {
            return false;
        }

        foreach (var rule in rules)
        {
            if (!string.IsNullOrWhiteSpace(rule.TargetType)
                && !string.Equals(rule.TargetType, "*", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(rule.TargetType, target.Type, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (rule.Labels.All(pair =>
                    target.Labels.TryGetValue(pair.Key, out var value)
                    && string.Equals(value, pair.Value, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record PolicyRule(string TargetType, IReadOnlyDictionary<string, string> Labels);

    private static TargetSystem Map(TargetUpsertDto dto)
        => new(
            dto.Id,
            dto.Name,
            dto.Host,
            dto.Port,
            dto.Labels,
            dto.Type,
            dto.Environment,
            dto.Criticality,
            dto.Status
        );
}
