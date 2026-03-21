using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class PolicyCacheTests
{
    private static AccessPolicyEvaluator CreateEvaluator(
        IPolicyStore policyStore,
        AccessOptions? options = null)
    {
        var opts = options ?? new AccessOptions
        {
            RolePolicyIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Admin"] = new() { "POL-1" }
            }
        };
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new AccessPolicyEvaluator(Options.Create(opts), policyStore, cache);
    }

    private static ClaimsPrincipal CreateUser(string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public void CachedEvaluation_ReturnsSameResult_WithoutQueryingStoreAgain()
    {
        var store = Substitute.For<IPolicyStore>();
        store.GetAll().Returns(new List<Policy>
        {
            new("POL-1", "Allow SSH", "Linux Server", "SSH", "Allow", null)
        });

        var evaluator = CreateEvaluator(store);
        var user = CreateUser("Admin");
        var target = new TargetSystem("t1", "Server", "10.0.0.1", 22, null, "Linux Server", "prod", "critical", "active");

        evaluator.IsSessionAllowed(user, target, "SSH", out _).Should().BeTrue();
        evaluator.IsSessionAllowed(user, target, "SSH", out _).Should().BeTrue();
        evaluator.IsSessionAllowed(user, target, "SSH", out _).Should().BeTrue();

        store.Received(1).GetAll();
    }

    [Fact]
    public void InvalidateCache_ForcesRefresh()
    {
        var store = Substitute.For<IPolicyStore>();
        store.GetAll().Returns(new List<Policy>
        {
            new("POL-1", "Allow SSH", "Linux Server", "SSH", "Allow", null)
        });

        var evaluator = CreateEvaluator(store);
        var user = CreateUser("Admin");
        var target = new TargetSystem("t1", "Server", "10.0.0.1", 22, null, "Linux Server", "prod", "critical", "active");

        evaluator.IsSessionAllowed(user, target, "SSH", out _);
        store.Received(1).GetAll();

        evaluator.InvalidateCache();
        evaluator.IsSessionAllowed(user, target, "SSH", out _);
        store.Received(2).GetAll();
    }

    [Fact]
    public void DenyPolicy_OverridesAllow()
    {
        var store = Substitute.For<IPolicyStore>();
        store.GetAll().Returns(new List<Policy>
        {
            new("POL-1", "Allow All", "Linux Server", "SSH,RDP", "Allow", null),
            new("POL-DENY", "Deny Prod", "Linux Server", "SSH", "Deny",
                new Dictionary<string, string> { ["env"] = "prod" })
        });

        var opts = new AccessOptions
        {
            RolePolicyIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Admin"] = new() { "POL-1", "POL-DENY" }
            }
        };
        var evaluator = CreateEvaluator(store, opts);
        var user = CreateUser("Admin");
        var target = new TargetSystem("t1", "ProdServer", "10.0.0.1", 22,
            new Dictionary<string, string> { ["env"] = "prod" },
            "Linux Server", "prod", "critical", "active");

        evaluator.IsSessionAllowed(user, target, "SSH", out var reason).Should().BeFalse();
        reason.Should().Contain("denied by policy");
    }
}
