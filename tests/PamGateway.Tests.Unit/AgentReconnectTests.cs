using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PamGateway.Agent;
using AgentWorker = PamGateway.Agent.Worker;
using AgentOpts = PamGateway.Agent.AgentOptions;

namespace PamGateway.Tests.Unit;

public sealed class AgentReconnectTests
{
    private static (AgentWorker worker, MockHttpHandler handler) CreateWorker(AgentOpts? options = null)
    {
        var opts = options ?? new AgentOpts
        {
            ApiBaseUrl = "http://localhost:8080",
            AgentId = "reconnect-agent",
            Hostname = "test-host",
            Os = "linux",
            JoinToken = "test-token",
            PublicUrl = "http://agent:7071"
        };

        var handler = new MockHttpHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(opts.ApiBaseUrl) };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("PamGateway").Returns(httpClient);

        var worker = new AgentWorker(
            NullLogger<AgentWorker>.Instance,
            factory,
            Options.Create(opts));

        return (worker, handler);
    }

    [Fact]
    public async Task Agent_ReRegisters_After3ConsecutiveHeartbeatFailures()
    {
        var (worker, handler) = CreateWorker();
        int heartbeatCallCount = 0;
        int registerCallCount = 0;

        handler.SetupDynamicResponse("/api/v1/agents/register", _ =>
        {
            registerCallCount++;
            return (HttpStatusCode.OK, JsonSerializer.Serialize(new AgentRegisterResponse
            {
                AgentToken = $"token-{registerCallCount}",
                AgentCert = "",
                HeartbeatIntervalSec = 1
            }));
        });

        handler.SetupDynamicResponse("/api/v1/agents/heartbeat", _ =>
        {
            heartbeatCallCount++;
            if (heartbeatCallCount >= 2 && heartbeatCallCount <= 4)
                return (HttpStatusCode.ServiceUnavailable, "{}");
            return (HttpStatusCode.OK, "{}");
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try { await worker.StartAsync(cts.Token); await Task.Delay(10000, cts.Token); }
        catch (OperationCanceledException) { }
        await worker.StopAsync(CancellationToken.None);

        registerCallCount.Should().BeGreaterThanOrEqualTo(2,
            "agent should re-register after 3 consecutive heartbeat failures");
    }

    [Fact]
    public async Task GracefulShutdown_SendsOfflineStatus()
    {
        var (worker, handler) = CreateWorker();
        handler.SetupResponse("/api/v1/agents/register", HttpStatusCode.OK,
            new AgentRegisterResponse { AgentToken = "tok-1", AgentCert = "", HeartbeatIntervalSec = 60 });
        handler.SetupResponse("/api/v1/agents/heartbeat", HttpStatusCode.OK, new { });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await worker.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        await worker.StopAsync(CancellationToken.None);

        var shutdownHb = handler.Requests
            .Where(r => r.path == "/api/v1/agents/heartbeat")
            .LastOrDefault();

        shutdownHb.body.Should().Contain("offline",
            "the last heartbeat during shutdown should send offline status");
    }
}
