using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Api.Controllers;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class HealthControllerTests
{
    private IServiceProvider CreateServiceProvider(
        bool includeTargets = true,
        bool includeRequests = true,
        bool includeSessions = true,
        bool includeAgents = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        if (includeTargets) services.AddSingleton<ITargetStore, InMemoryTargetStore>();
        if (includeRequests) services.AddSingleton<IAccessRequestStore, InMemoryAccessRequestStore>();
        if (includeSessions) services.AddSingleton<ISessionStore, InMemorySessionStore>();
        if (includeAgents) services.AddSingleton<IAgentStore, InMemoryAgentStore>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Get_ReturnsOk()
    {
        var controller = new HealthController(CreateServiceProvider());
        var result = controller.Get() as OkObjectResult;
        result.Should().NotBeNull();
    }

    [Fact]
    public void Liveness_ReturnsAlive()
    {
        var controller = new HealthController(CreateServiceProvider());
        var result = controller.Liveness() as OkObjectResult;
        result.Should().NotBeNull();
    }

    [Fact]
    public void Readiness_AllStoresPresent_ReturnsReady()
    {
        var controller = new HealthController(CreateServiceProvider());
        var result = controller.Readiness() as OkObjectResult;
        result.Should().NotBeNull();
    }

    [Fact]
    public void Readiness_MissingStore_Returns503()
    {
        var controller = new HealthController(CreateServiceProvider(includeTargets: false));
        var result = controller.Readiness();
        var statusResult = result as ObjectResult;
        statusResult.Should().NotBeNull();
        statusResult!.StatusCode.Should().Be(503);
    }
}
