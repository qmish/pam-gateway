using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class AccessPolicyEvaluatorTests
{
    private readonly IPolicyStore _policyStore = Substitute.For<IPolicyStore>();

    private AccessPolicyEvaluator CreateEvaluator(AccessOptions options)
        => new(Options.Create(options), _policyStore);

    private static ClaimsPrincipal CreateUser(params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "testuser") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static ClaimsPrincipal AnonymousUser()
        => new(new ClaimsIdentity());

    private static TargetSystem CreateTarget(
        string type = "Windows",
        Dictionary<string, string>? labels = null)
        => new("T1", "TestTarget", "10.0.0.1", 3389, labels, type, "prod", "critical", "active");

    [Fact]
    public void UnauthenticatedUser_AlwaysAllowed()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PAM_Administrator"] = ["policy1"]
            }
        };
        var evaluator = CreateEvaluator(options);
        var target = CreateTarget();

        evaluator.IsRequestAllowed(AnonymousUser(), target, out _).Should().BeTrue();
    }

    [Fact]
    public void NoRolePolicyIds_AlwaysAllowed()
    {
        var evaluator = CreateEvaluator(new AccessOptions());
        var user = CreateUser("PAM_Administrator");
        var target = CreateTarget();

        evaluator.IsRequestAllowed(user, target, out _).Should().BeTrue();
    }

    [Fact]
    public void UserWithNoMatchingRole_Denied()
    {
        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PAM_Administrator"] = ["policy1"]
            }
        };
        var evaluator = CreateEvaluator(options);
        var user = CreateUser("SomeOtherRole");

        evaluator.IsRequestAllowed(user, CreateTarget(), out var reason).Should().BeFalse();
        reason.Should().Contain("No matching policies");
    }

    [Fact]
    public void AllowPolicy_MatchesTarget_Allowed()
    {
        var policy = new Policy("p1", "AllowWindows", "Windows", "rdp,ssh", "allow", null);
        _policyStore.GetById("p1").Returns(policy);

        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SysAdmin"] = ["p1"]
            }
        };
        var evaluator = CreateEvaluator(options);
        var user = CreateUser("SysAdmin");

        evaluator.IsRequestAllowed(user, CreateTarget(type: "Windows"), out _).Should().BeTrue();
    }

    [Fact]
    public void AllowPolicy_WrongTargetType_Denied()
    {
        var policy = new Policy("p1", "AllowLinux", "Linux", "ssh", "allow", null);
        _policyStore.GetById("p1").Returns(policy);

        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SysAdmin"] = ["p1"]
            }
        };
        var evaluator = CreateEvaluator(options);
        var user = CreateUser("SysAdmin");

        evaluator.IsRequestAllowed(user, CreateTarget(type: "Windows"), out var reason).Should().BeFalse();
        reason.Should().Contain("No policies matched target");
    }

    [Fact]
    public void DenyPolicy_MatchesTarget_Denied()
    {
        var policy = new Policy("p1", "DenyAll", "Windows", "*", "deny", null);
        _policyStore.GetById("p1").Returns(policy);

        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SysAdmin"] = ["p1"]
            }
        };
        var evaluator = CreateEvaluator(options);
        var user = CreateUser("SysAdmin");

        evaluator.IsRequestAllowed(user, CreateTarget(), out var reason).Should().BeFalse();
        reason.Should().Contain("denied by policy");
    }

    [Fact]
    public void DenyOverridesAllow()
    {
        var allow = new Policy("p1", "AllowWindows", "Windows", "*", "allow", null);
        var deny = new Policy("p2", "DenyWindows", "Windows", "*", "deny", null);
        _policyStore.GetById("p1").Returns(allow);
        _policyStore.GetById("p2").Returns(deny);

        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SysAdmin"] = ["p1", "p2"]
            }
        };
        var evaluator = CreateEvaluator(options);
        var user = CreateUser("SysAdmin");

        evaluator.IsRequestAllowed(user, CreateTarget(), out _).Should().BeFalse();
    }

    [Fact]
    public void SessionAllowed_ChecksProtocol()
    {
        var policy = new Policy("p1", "AllowRDP", "Windows", "rdp", "allow", null);
        _policyStore.GetById("p1").Returns(policy);

        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SysAdmin"] = ["p1"]
            }
        };
        var evaluator = CreateEvaluator(options);
        var user = CreateUser("SysAdmin");

        evaluator.IsSessionAllowed(user, CreateTarget(), "rdp", out _).Should().BeTrue();
        evaluator.IsSessionAllowed(user, CreateTarget(), "ssh", out _).Should().BeFalse();
    }

    [Fact]
    public void Policy_WildcardProtocol_AllowsAny()
    {
        var policy = new Policy("p1", "AllowAll", "Windows", "*", "allow", null);
        _policyStore.GetById("p1").Returns(policy);

        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SysAdmin"] = ["p1"]
            }
        };
        var evaluator = CreateEvaluator(options);
        var user = CreateUser("SysAdmin");

        evaluator.IsSessionAllowed(user, CreateTarget(), "rdp", out _).Should().BeTrue();
        evaluator.IsSessionAllowed(user, CreateTarget(), "ssh", out _).Should().BeTrue();
    }

    [Fact]
    public void Policy_WildcardTargetType_MatchesAny()
    {
        var policy = new Policy("p1", "AllowAnyType", "*", "ssh", "allow", null);
        _policyStore.GetById("p1").Returns(policy);

        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SysAdmin"] = ["p1"]
            }
        };
        var evaluator = CreateEvaluator(options);
        var user = CreateUser("SysAdmin");

        evaluator.IsSessionAllowed(user, CreateTarget(type: "Linux"), "ssh", out _).Should().BeTrue();
        evaluator.IsSessionAllowed(user, CreateTarget(type: "Windows"), "ssh", out _).Should().BeTrue();
    }

    [Fact]
    public void Policy_LabelSelector_Matches()
    {
        var labels = new Dictionary<string, string> { ["env"] = "prod" };
        var policy = new Policy("p1", "ProdOnly", "Windows", "*", "allow", labels);
        _policyStore.GetById("p1").Returns(policy);

        var options = new AccessOptions
        {
            RolePolicyIds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SysAdmin"] = ["p1"]
            }
        };
        var evaluator = CreateEvaluator(options);
        var user = CreateUser("SysAdmin");

        var prodTarget = CreateTarget(labels: new() { ["env"] = "prod" });
        var devTarget = CreateTarget(labels: new() { ["env"] = "dev" });
        var noLabelsTarget = CreateTarget(labels: null);

        evaluator.IsRequestAllowed(user, prodTarget, out _).Should().BeTrue();
        evaluator.IsRequestAllowed(user, devTarget, out _).Should().BeFalse();
        evaluator.IsRequestAllowed(user, noLabelsTarget, out _).Should().BeFalse();
    }
}
