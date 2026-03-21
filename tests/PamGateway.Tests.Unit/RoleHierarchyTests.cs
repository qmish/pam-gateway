using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class RoleHierarchyTests
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
        var claims = new List<Claim> { new("sub", "u1"), new(ClaimTypes.Name, "testuser") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static TargetSystem Target(string type = "Windows")
        => new("T1", "Target", "10.0.0.1", 3389, null, type, "prod", "critical", "active");

    [Fact]
    public void ChildRole_InheritsParentPolicies()
    {
        var parentPolicy = new Policy("p-parent", "ParentAllow", "Windows", "*", "allow", null);
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["BaseRole"] = ["p-parent"]
            },
            RoleHierarchy = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ChildRole"] = "BaseRole"
            }
        };

        var evaluator = CreateEvaluator(options, parentPolicy);
        var user = CreateUser("ChildRole");

        evaluator.IsRequestAllowed(user, Target(), out _).Should().BeTrue();
        evaluator.LastDecision!.MatchedPolicyIds.Should().Contain("p-parent");
    }

    [Fact]
    public void MultiLevelHierarchy_InheritsAll()
    {
        var rootPolicy = new Policy("p-root", "RootAllow", "Windows", "*", "allow", null);
        var midPolicy = new Policy("p-mid", "MidAllow", "Linux", "*", "allow", null);
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Root"] = ["p-root"],
                ["Mid"] = ["p-mid"]
            },
            RoleHierarchy = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Leaf"] = "Mid",
                ["Mid"] = "Root"
            }
        };

        var evaluator = CreateEvaluator(options, rootPolicy, midPolicy);
        var user = CreateUser("Leaf");

        evaluator.IsRequestAllowed(user, Target(type: "Windows"), out _).Should().BeTrue();
    }

    [Fact]
    public void ChildAndParent_NoDuplicatePolicies()
    {
        var shared = new Policy("p-shared", "SharedAllow", "Windows", "*", "allow", null);
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Parent"] = ["p-shared"],
                ["Child"] = ["p-shared"]
            },
            RoleHierarchy = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Child"] = "Parent"
            }
        };

        var evaluator = CreateEvaluator(options, shared);
        var user = CreateUser("Child");

        evaluator.IsRequestAllowed(user, Target(), out _).Should().BeTrue();
        evaluator.LastDecision!.MatchedPolicyIds.Should().HaveCount(1);
    }

    [Fact]
    public void NoHierarchy_WorksAsUsual()
    {
        var policy = new Policy("p1", "Allow", "Windows", "*", "allow", null);
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase) { ["Admin"] = ["p1"] }
        };

        var evaluator = CreateEvaluator(options, policy);
        var user = CreateUser("Admin");

        evaluator.IsRequestAllowed(user, Target(), out _).Should().BeTrue();
    }

    [Fact]
    public void DenyFromParent_BlocksChild()
    {
        var allowChild = new Policy("p-allow", "AllowChild", "Windows", "*", "allow", null);
        var denyParent = new Policy("p-deny", "DenyParent", "Windows", "*", "deny", null);
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Child"] = ["p-allow"],
                ["Parent"] = ["p-deny"]
            },
            RoleHierarchy = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Child"] = "Parent"
            }
        };

        var evaluator = CreateEvaluator(options, allowChild, denyParent);
        var user = CreateUser("Child");

        evaluator.IsRequestAllowed(user, Target(), out _).Should().BeFalse();
        evaluator.LastDecision!.DenyPolicyId.Should().Be("p-deny");
    }
}
