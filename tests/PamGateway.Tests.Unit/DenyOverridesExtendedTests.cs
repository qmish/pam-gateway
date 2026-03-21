using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class DenyOverridesExtendedTests
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

    private static TargetSystem Target(string type = "Windows", Dictionary<string, string>? labels = null)
        => new("T1", "Target", "10.0.0.1", 3389,
            labels ?? new Dictionary<string, string> { ["env"] = "prod" },
            type, "prod", "critical", "active");

    [Fact]
    public void DenyAlwaysWins_RegardlessOfOrder()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new List<string> { "allow1", "allow2", "allow3", "deny1" }
            }
        };

        var policies = new[]
        {
            new Policy("allow1", "A1", "Windows", "*", "Allow", new Dictionary<string, string> { ["env"] = "prod" }),
            new Policy("allow2", "A2", "Windows", "*", "Allow", new Dictionary<string, string> { ["env"] = "prod" }),
            new Policy("allow3", "A3", "Windows", "*", "Allow", new Dictionary<string, string> { ["env"] = "prod" }),
            new Policy("deny1", "D1", "Windows", "*", "Deny", new Dictionary<string, string> { ["env"] = "prod" }),
        };

        var evaluator = CreateEvaluator(options, policies);
        evaluator.IsRequestAllowed(CreateUser("admin"), Target(), out var reason).Should().BeFalse();
        reason.Should().Contain("denied");
        evaluator.LastDecision!.DenyPolicyId.Should().Be("deny1");
        evaluator.LastDecision.MatchedPolicyIds.Should().HaveCount(4);
    }

    [Fact]
    public void DenyOnDifferentProtocol_DoesNotBlockOther()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new List<string> { "allow_ssh", "deny_rdp" }
            }
        };

        var allowSsh = new Policy("allow_ssh", "AllowSSH", "Linux", "ssh", "Allow", null);
        var denyRdp = new Policy("deny_rdp", "DenyRDP", "Linux", "rdp", "Deny", null);

        var target = Target("Linux");
        var evaluator = CreateEvaluator(options, allowSsh, denyRdp);

        evaluator.IsSessionAllowed(CreateUser("admin"), target, "ssh", out _).Should().BeTrue();
        evaluator.IsSessionAllowed(CreateUser("admin"), target, "rdp", out _).Should().BeFalse();
    }

    [Fact]
    public void DenyWithWildcardProtocol_BlocksAll()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new List<string> { "allow_all", "deny_all" }
            }
        };

        var allow = new Policy("allow_all", "AllowAll", "Linux", "*", "Allow", null);
        var deny = new Policy("deny_all", "DenyAll", "Linux", "*", "Deny", null);

        var evaluator = CreateEvaluator(options, allow, deny);
        var target = Target("Linux");

        evaluator.IsSessionAllowed(CreateUser("admin"), target, "ssh", out _).Should().BeFalse();
        evaluator.IsSessionAllowed(CreateUser("admin"), target, "rdp", out _).Should().BeFalse();
    }

    [Fact]
    public void DenySpecificLabel_AllowGeneral()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new List<string> { "allow_general", "deny_prod" }
            }
        };

        var allowGeneral = new Policy("allow_general", "AllowGeneral", "Windows", "*", "Allow", null);
        var denyProd = new Policy("deny_prod", "DenyProd", "Windows", "*", "Deny",
            new Dictionary<string, string> { ["env"] = "prod" });

        var evaluator = CreateEvaluator(options, allowGeneral, denyProd);

        evaluator.IsRequestAllowed(CreateUser("admin"), Target(), out _).Should().BeFalse();

        var devTarget = Target(labels: new Dictionary<string, string> { ["env"] = "dev" });
        evaluator.IsRequestAllowed(CreateUser("admin"), devTarget, out _).Should().BeTrue();
    }

    [Fact]
    public void AuditDecision_ContainsUserId()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new List<string> { "allow1" }
            }
        };
        var allow = new Policy("allow1", "A", "Windows", "*", "Allow", null);
        var evaluator = CreateEvaluator(options, allow);

        evaluator.IsRequestAllowed(CreateUser("admin"), Target(), out _);

        evaluator.LastDecision.Should().NotBeNull();
        evaluator.LastDecision!.UserId.Should().Be("user1");
        evaluator.LastDecision.TargetId.Should().Be("T1");
        evaluator.LastDecision.Allowed.Should().BeTrue();
    }

    [Fact]
    public void AuditDecision_DenyIncludesReason()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new List<string> { "deny1" }
            }
        };
        var deny = new Policy("deny1", "D", "Windows", "*", "Deny", null);
        var evaluator = CreateEvaluator(options, deny);

        evaluator.IsRequestAllowed(CreateUser("admin"), Target(), out var reason);

        evaluator.LastDecision!.Allowed.Should().BeFalse();
        evaluator.LastDecision.Reason.Should().Contain("denied");
        evaluator.LastDecision.DenyPolicyId.Should().Be("deny1");
        reason.Should().Contain("denied");
    }

    [Fact]
    public void AuditDecision_NoMatchingPolicies()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new List<string> { "allow1" }
            }
        };
        var allow = new Policy("allow1", "A", "Linux", "*", "Allow", null);
        var evaluator = CreateEvaluator(options, allow);

        evaluator.IsRequestAllowed(CreateUser("admin"), Target("Windows"), out _);

        evaluator.LastDecision!.Allowed.Should().BeFalse();
        evaluator.LastDecision.Reason.Should().Contain("No policies matched");
        evaluator.LastDecision.MatchedPolicyIds.Should().BeEmpty();
    }

    [Fact]
    public void MultipleRoles_DenyFromAnyRoleBlocks()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["viewer"] = new List<string> { "allow1" },
                ["restricted"] = new List<string> { "deny1" }
            }
        };

        var allow = new Policy("allow1", "A", "Windows", "*", "Allow", null);
        var deny = new Policy("deny1", "D", "Windows", "*", "Deny", null);
        var evaluator = CreateEvaluator(options, allow, deny);

        evaluator.IsRequestAllowed(CreateUser("viewer", "restricted"), Target(), out _).Should().BeFalse();
    }

    [Fact]
    public void InheritedDeny_BlocksChildRole()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["child"] = new List<string> { "allow1" },
                ["parent"] = new List<string> { "deny1" }
            },
            RoleHierarchy = new(StringComparer.OrdinalIgnoreCase) { ["child"] = "parent" }
        };

        var allow = new Policy("allow1", "A", "Windows", "*", "Allow", null);
        var deny = new Policy("deny1", "D", "Windows", "*", "Deny", null);
        var evaluator = CreateEvaluator(options, allow, deny);

        evaluator.IsRequestAllowed(CreateUser("child"), Target(), out _).Should().BeFalse();
        evaluator.LastDecision!.DenyPolicyId.Should().Be("deny1");
    }
}
