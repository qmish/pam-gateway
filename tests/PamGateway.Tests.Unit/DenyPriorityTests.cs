using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class DenyPriorityTests
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
        var claims = new List<Claim> { new("sub", "user1"), new(ClaimTypes.Name, "testuser") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static TargetSystem CreateTarget(Dictionary<string, string>? labels = null)
        => new("T1", "TestTarget", "10.0.0.1", 3389, labels ?? new Dictionary<string, string> { ["env"] = "prod" },
            "Windows", "prod", "critical", "active");

    [Fact]
    public void DenyAfterAllow_StillDenied()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new List<string> { "allow1", "deny1" }
            }
        };

        var allowPolicy = new Policy("allow1", "AllowAll", "Windows", "*", "Allow",
            new Dictionary<string, string> { ["env"] = "prod" });
        var denyPolicy = new Policy("deny1", "DenyAll", "Windows", "*", "Deny",
            new Dictionary<string, string> { ["env"] = "prod" });

        var evaluator = CreateEvaluator(options, allowPolicy, denyPolicy);
        var result = evaluator.IsRequestAllowed(CreateUser("admin"), CreateTarget(), out var reason);

        result.Should().BeFalse();
        reason.Should().Contain("denied");
        evaluator.LastDecision.Should().NotBeNull();
        evaluator.LastDecision!.DenyPolicyId.Should().Be("deny1");
        evaluator.LastDecision.MatchedPolicyIds.Should().Contain("allow1");
        evaluator.LastDecision.MatchedPolicyIds.Should().Contain("deny1");
    }

    [Fact]
    public void DenyBeforeAllow_StillDenied()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new List<string> { "deny1", "allow1" }
            }
        };

        var denyPolicy = new Policy("deny1", "DenyAll", "Windows", "*", "Deny",
            new Dictionary<string, string> { ["env"] = "prod" });
        var allowPolicy = new Policy("allow1", "AllowAll", "Windows", "*", "Allow",
            new Dictionary<string, string> { ["env"] = "prod" });

        var evaluator = CreateEvaluator(options, denyPolicy, allowPolicy);
        var result = evaluator.IsRequestAllowed(CreateUser("admin"), CreateTarget(), out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void MultipleDenies_FirstRecorded()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new List<string> { "allow1", "deny1", "deny2" }
            }
        };

        var allowPolicy = new Policy("allow1", "A", "Windows", "*", "Allow",
            new Dictionary<string, string> { ["env"] = "prod" });
        var denyPolicy1 = new Policy("deny1", "D1", "Windows", "*", "Deny",
            new Dictionary<string, string> { ["env"] = "prod" });
        var denyPolicy2 = new Policy("deny2", "D2", "Windows", "*", "Deny",
            new Dictionary<string, string> { ["env"] = "prod" });

        var evaluator = CreateEvaluator(options, allowPolicy, denyPolicy1, denyPolicy2);
        evaluator.IsRequestAllowed(CreateUser("admin"), CreateTarget(), out _);

        evaluator.LastDecision!.DenyPolicyId.Should().Be("deny1");
        evaluator.LastDecision.MatchedPolicyIds.Should().HaveCount(3);
    }

    [Fact]
    public void OnlyAllowPolicies_Allowed()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new List<string> { "allow1", "allow2" }
            }
        };

        var p1 = new Policy("allow1", "A1", "Windows", "*", "Allow",
            new Dictionary<string, string> { ["env"] = "prod" });
        var p2 = new Policy("allow2", "A2", "Windows", "*", "Allow",
            new Dictionary<string, string> { ["env"] = "prod" });

        var evaluator = CreateEvaluator(options, p1, p2);
        evaluator.IsRequestAllowed(CreateUser("admin"), CreateTarget(), out _).Should().BeTrue();
        evaluator.LastDecision!.Allowed.Should().BeTrue();
    }

    [Fact]
    public void DenyFromInheritedRole_BlocksAccess()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["child"] = new List<string> { "allow1" },
                ["parent"] = new List<string> { "deny1" }
            },
            RoleHierarchy = new(StringComparer.OrdinalIgnoreCase)
            {
                ["child"] = "parent"
            }
        };

        var allowPolicy = new Policy("allow1", "AllowChild", "Windows", "*", "Allow",
            new Dictionary<string, string> { ["env"] = "prod" });
        var denyPolicy = new Policy("deny1", "DenyParent", "Windows", "*", "Deny",
            new Dictionary<string, string> { ["env"] = "prod" });

        var evaluator = CreateEvaluator(options, allowPolicy, denyPolicy);
        evaluator.IsRequestAllowed(CreateUser("child"), CreateTarget(), out _).Should().BeFalse();
        evaluator.LastDecision!.DenyPolicyId.Should().Be("deny1");
    }

    [Fact]
    public void DenyOnProtocol_BlocksSpecificProtocol()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new List<string> { "allow1", "deny_rdp" }
            }
        };

        var allowPolicy = new Policy("allow1", "AllowSSH", "Windows", "ssh", "Allow",
            new Dictionary<string, string> { ["env"] = "prod" });
        var denyPolicy = new Policy("deny_rdp", "DenyRDP", "Windows", "rdp", "Deny",
            new Dictionary<string, string> { ["env"] = "prod" });

        var evaluator = CreateEvaluator(options, allowPolicy, denyPolicy);

        evaluator.IsSessionAllowed(CreateUser("admin"), CreateTarget(), "ssh", out _)
            .Should().BeTrue();

        evaluator.IsSessionAllowed(CreateUser("admin"), CreateTarget(), "rdp", out _)
            .Should().BeFalse();
    }

    [Fact]
    public void AllMatchedPoliciesRecordedInAudit()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new List<string> { "p1", "p2", "p3" }
            }
        };

        var p1 = new Policy("p1", "A", "*", "*", "Allow", null);
        var p2 = new Policy("p2", "B", "*", "*", "Deny", null);
        var p3 = new Policy("p3", "C", "*", "*", "Allow", null);

        var evaluator = CreateEvaluator(options, p1, p2, p3);
        evaluator.IsRequestAllowed(CreateUser("admin"), CreateTarget(), out _);

        evaluator.LastDecision!.MatchedPolicyIds.Should().BeEquivalentTo(["p1", "p2", "p3"]);
        evaluator.LastDecision.DenyPolicyId.Should().Be("p2");
    }
}
