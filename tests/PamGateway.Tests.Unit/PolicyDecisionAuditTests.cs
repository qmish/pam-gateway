using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class PolicyDecisionAuditTests
{
    private AccessPolicyEvaluator CreateEvaluator(AccessOptions options, params Policy[] policies)
    {
        var store = Substitute.For<IPolicyStore>();
        store.GetAll().Returns(policies.ToList());
        foreach (var p in policies) store.GetById(p.Id).Returns(p);
        return new(Options.Create(options), store, new MemoryCache(new MemoryCacheOptions()));
    }

    private static ClaimsPrincipal CreateUser(params string[] roles)
    {
        var claims = new List<Claim>
        {
            new("sub", "user-42"),
            new(ClaimTypes.Name, "testuser")
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static TargetSystem Target(string id = "T1", string type = "Windows")
        => new(id, "TestTarget", "10.0.0.1", 3389, null, type, "prod", "critical", "active");

    [Fact]
    public void AllowDecision_ContainsMatchedPolicyIds()
    {
        var policy = new Policy("p1", "AllowWin", "Windows", "*", "allow", null);
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase) { ["Admin"] = ["p1"] }
        };
        var evaluator = CreateEvaluator(options, policy);
        var user = CreateUser("Admin");

        evaluator.IsRequestAllowed(user, Target(), out _).Should().BeTrue();
        evaluator.LastDecision.Should().NotBeNull();
        evaluator.LastDecision!.Allowed.Should().BeTrue();
        evaluator.LastDecision.MatchedPolicyIds.Should().Contain("p1");
        evaluator.LastDecision.DenyPolicyId.Should().BeNull();
        evaluator.LastDecision.UserId.Should().Be("user-42");
    }

    [Fact]
    public void DenyDecision_ContainsDenyPolicyId()
    {
        var deny = new Policy("p-deny", "DenyAll", "Windows", "*", "deny", null);
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase) { ["Admin"] = ["p-deny"] }
        };
        var evaluator = CreateEvaluator(options, deny);
        var user = CreateUser("Admin");

        evaluator.IsRequestAllowed(user, Target(), out _).Should().BeFalse();
        evaluator.LastDecision.Should().NotBeNull();
        evaluator.LastDecision!.Allowed.Should().BeFalse();
        evaluator.LastDecision.DenyPolicyId.Should().Be("p-deny");
    }

    [Fact]
    public void NoMatchingPolicies_AuditShowsReason()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase) { ["Admin"] = ["p1"] }
        };
        var evaluator = CreateEvaluator(options);
        var user = CreateUser("Admin");

        evaluator.IsRequestAllowed(user, Target(), out _).Should().BeFalse();
        evaluator.LastDecision.Should().NotBeNull();
        evaluator.LastDecision!.Allowed.Should().BeFalse();
        evaluator.LastDecision.Reason.Should().Contain("No matching policies");
    }

    [Fact]
    public void UnauthenticatedUser_BypassAudit()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase) { ["Admin"] = ["p1"] }
        };
        var evaluator = CreateEvaluator(options);
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        evaluator.IsRequestAllowed(anonymous, Target(), out _).Should().BeTrue();
        evaluator.LastDecision.Should().NotBeNull();
        evaluator.LastDecision!.Allowed.Should().BeTrue();
        evaluator.LastDecision.Reason.Should().Be("unauthenticated_bypass");
    }

    [Fact]
    public void SessionDecision_IncludesProtocol()
    {
        var policy = new Policy("p1", "AllowRDP", "Windows", "rdp", "allow", null);
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase) { ["Admin"] = ["p1"] }
        };
        var evaluator = CreateEvaluator(options, policy);
        var user = CreateUser("Admin");

        evaluator.IsSessionAllowed(user, Target(), "rdp", out _).Should().BeTrue();
        evaluator.LastDecision!.Protocol.Should().Be("rdp");
        evaluator.LastDecision.TargetId.Should().Be("T1");
    }

    [Fact]
    public void MultiplePolicies_AllMatchedIdsRecorded()
    {
        var p1 = new Policy("p1", "AllowOne", "Windows", "*", "allow", null);
        var p2 = new Policy("p2", "AllowTwo", "Windows", "*", "allow", null);
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase) { ["Admin"] = ["p1", "p2"] }
        };
        var evaluator = CreateEvaluator(options, p1, p2);
        var user = CreateUser("Admin");

        evaluator.IsRequestAllowed(user, Target(), out _).Should().BeTrue();
        evaluator.LastDecision!.MatchedPolicyIds.Should().BeEquivalentTo(new[] { "p1", "p2" });
    }
}
