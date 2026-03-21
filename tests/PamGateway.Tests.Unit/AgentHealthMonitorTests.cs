using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Api.Services;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class AgentHealthMonitorTests
{
    private readonly InMemoryAgentStore _agentStore = new();
    private readonly InMemoryAuditStore _auditStore = new();

    private AgentHealthMonitorService CreateService(AgentHealthMonitorOptions? opts = null)
    {
        opts ??= new AgentHealthMonitorOptions
        {
            CheckIntervalSeconds = 1,
            OfflineThresholdSeconds = 60
        };

        var services = new ServiceCollection();
        services.AddSingleton<IAgentStore>(_agentStore);
        services.AddSingleton<IAuditStore>(_auditStore);
        var sp = services.BuildServiceProvider();

        return new AgentHealthMonitorService(
            sp,
            Options.Create(opts),
            Substitute.For<ILogger<AgentHealthMonitorService>>());
    }

    [Fact]
    public void CheckAgentHealth_MarksStaleAgentOffline()
    {
        var agent = new AgentInfo("agent-1", "host-1", "linux", AgentStatus.Online,
            DateTimeOffset.UtcNow.AddMinutes(-5), "http://agent:7071",
            new Dictionary<string, string>(), Array.Empty<string>(), "token");
        _agentStore.Register(agent);

        var svc = CreateService(new AgentHealthMonitorOptions
        {
            CheckIntervalSeconds = 1,
            OfflineThresholdSeconds = 60
        });
        svc.CheckAgentHealth();

        var updated = _agentStore.GetById("agent-1")!;
        updated.Status.Should().Be(AgentStatus.Offline);
    }

    [Fact]
    public void CheckAgentHealth_DoesNotAffectRecentAgents()
    {
        var agent = new AgentInfo("agent-2", "host-2", "linux", AgentStatus.Online,
            DateTimeOffset.UtcNow, "http://agent:7071",
            new Dictionary<string, string>(), Array.Empty<string>(), "token");
        _agentStore.Register(agent);

        var svc = CreateService(new AgentHealthMonitorOptions
        {
            CheckIntervalSeconds = 1,
            OfflineThresholdSeconds = 60
        });
        svc.CheckAgentHealth();

        var updated = _agentStore.GetById("agent-2")!;
        updated.Status.Should().Be(AgentStatus.Online);
    }

    [Fact]
    public void CheckAgentHealth_SkipsAlreadyOfflineAgents()
    {
        var agent = new AgentInfo("agent-3", "host-3", "linux", AgentStatus.Offline,
            DateTimeOffset.UtcNow.AddMinutes(-10), "http://agent:7071",
            new Dictionary<string, string>(), Array.Empty<string>(), "token");
        _agentStore.Register(agent);

        var svc = CreateService();
        svc.CheckAgentHealth();

        _auditStore.GetAll().Should().NotContain(e => e.EventType == "agent.offline");
    }

    [Fact]
    public void CheckAgentHealth_WritesAuditEvent_WhenAgentGoesOffline()
    {
        var agent = new AgentInfo("agent-4", "host-4", "linux", AgentStatus.Online,
            DateTimeOffset.UtcNow.AddMinutes(-5), "http://agent:7071",
            new Dictionary<string, string>(), Array.Empty<string>(), "token");
        _agentStore.Register(agent);

        var svc = CreateService();
        svc.CheckAgentHealth();

        _auditStore.GetAll().Should().ContainSingle(e => e.EventType == "agent.offline");
        var evt = _auditStore.GetAll().First(e => e.EventType == "agent.offline");
        evt.TargetName.Should().Be("host-4");
    }
}
