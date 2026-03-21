using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PamGateway.Agent;
using AgentWorker = PamGateway.Agent.Worker;
using AgentOpts = PamGateway.Agent.AgentOptions;

namespace PamGateway.Tests.Unit;

public sealed class AgentWorkerTests
{
    private static (AgentWorker worker, MockHttpHandler handler) CreateWorker(AgentOpts? options = null)
    {
        var opts = options ?? new AgentOpts
        {
            ApiBaseUrl = "http://localhost:8080",
            AgentId = "test-agent",
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
    public async Task EnsureRegistered_SendsCorrectPayload()
    {
        var registerResponse = new AgentRegisterResponse
        {
            AgentToken = "token-123",
            AgentCert = "",
            HeartbeatIntervalSec = 30
        };

        var (worker, handler) = CreateWorker();
        handler.SetupResponse("/api/v1/agents/register", HttpStatusCode.OK, registerResponse);
        handler.SetupResponse("/api/v1/agents/heartbeat", HttpStatusCode.OK, new { });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await worker.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        await worker.StopAsync(CancellationToken.None);

        handler.Requests.Should().Contain(r => r.path == "/api/v1/agents/register");
        var registerReq = handler.Requests.First(r => r.path == "/api/v1/agents/register");
        registerReq.method.Should().Be("POST");
        registerReq.body.Should().Contain("test-agent");
    }

    [Fact]
    public async Task Heartbeat_SentAfterRegistration()
    {
        var registerResponse = new AgentRegisterResponse
        {
            AgentToken = "token-abc",
            AgentCert = "",
            HeartbeatIntervalSec = 1
        };

        var (worker, handler) = CreateWorker();
        handler.SetupResponse("/api/v1/agents/register", HttpStatusCode.OK, registerResponse);
        handler.SetupResponse("/api/v1/agents/heartbeat", HttpStatusCode.OK, new { });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await worker.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        await worker.StopAsync(CancellationToken.None);

        handler.Requests.Should().Contain(r => r.path == "/api/v1/agents/heartbeat");
        var hb = handler.Requests.First(r => r.path == "/api/v1/agents/heartbeat");
        hb.body.Should().Contain("test-agent");
        hb.body.Should().Contain("online");
    }

    [Fact]
    public async Task Heartbeat_IncludesBearerToken()
    {
        var registerResponse = new AgentRegisterResponse
        {
            AgentToken = "secret-token-xyz",
            AgentCert = "",
            HeartbeatIntervalSec = 1
        };

        var (worker, handler) = CreateWorker();
        handler.SetupResponse("/api/v1/agents/register", HttpStatusCode.OK, registerResponse);
        handler.SetupResponse("/api/v1/agents/heartbeat", HttpStatusCode.OK, new { });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await worker.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        await worker.StopAsync(CancellationToken.None);

        var hbReqs = handler.Requests.Where(r => r.path == "/api/v1/agents/heartbeat").ToList();
        hbReqs.Should().NotBeEmpty();
        hbReqs.First().authHeader.Should().Contain("secret-token-xyz");
    }

    [Fact]
    public async Task Register_RetriesOnFailure()
    {
        var (worker, handler) = CreateWorker();
        var callCount = 0;
        handler.SetupDynamicResponse("/api/v1/agents/register", _ =>
        {
            callCount++;
            if (callCount <= 2)
            {
                return (HttpStatusCode.ServiceUnavailable, "{}");
            }
            return (HttpStatusCode.OK, JsonSerializer.Serialize(new AgentRegisterResponse
            {
                AgentToken = "token-retry", AgentCert = "", HeartbeatIntervalSec = 60
            }));
        });
        handler.SetupResponse("/api/v1/agents/heartbeat", HttpStatusCode.OK, new { });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try { await worker.StartAsync(cts.Token); await Task.Delay(12000, cts.Token); }
        catch (OperationCanceledException) { }
        await worker.StopAsync(CancellationToken.None);

        var registerRequests = handler.Requests.Count(r => r.path == "/api/v1/agents/register");
        registerRequests.Should().BeGreaterThanOrEqualTo(3, "должно быть несколько попыток регистрации");
    }
}

public sealed class MockHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, (HttpStatusCode, string)>> _dynamicResponses = new();
    private readonly Dictionary<string, (HttpStatusCode code, string body)> _responses = new();
    public List<(string method, string path, string body, string? authHeader)> Requests { get; } = new();

    public void SetupResponse<T>(string path, HttpStatusCode code, T body)
    {
        _responses[path] = (code, JsonSerializer.Serialize(body));
    }

    public void SetupDynamicResponse(string path, Func<HttpRequestMessage, (HttpStatusCode, string)> handler)
    {
        _dynamicResponses[path] = handler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        var body = request.Content is not null ? await request.Content.ReadAsStringAsync(cancellationToken) : "";
        var auth = request.Headers.Authorization?.ToString();
        Requests.Add((request.Method.Method, path, body, auth));

        if (_dynamicResponses.TryGetValue(path, out var handler))
        {
            var (code, responseBody) = handler(request);
            return new HttpResponseMessage(code) { Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json") };
        }

        if (_responses.TryGetValue(path, out var response))
        {
            return new HttpResponseMessage(response.code) { Content = new StringContent(response.body, System.Text.Encoding.UTF8, "application/json") };
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
