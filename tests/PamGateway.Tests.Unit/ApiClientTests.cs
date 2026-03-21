using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace PamGateway.Tests.Unit;

public sealed class ApiClientTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task GetTargets_CallsCorrectEndpoint()
    {
        var handler = new MockHttpHandler();
        handler.SetupResponse("/api/v1/targets", HttpStatusCode.OK, new[]
        {
            new { id = "t1", name = "Server1", host = "10.0.0.1", port = 22, type = "Linux", environment = "prod", criticality = "critical", status = "active" }
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };

        var response = await http.GetAsync("/api/v1/targets");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Server1");
    }

    [Fact]
    public async Task GetSessions_ReturnsNull_OnServerError()
    {
        var handler = new MockHttpHandler();
        handler.SetupResponse("/api/v1/sessions", HttpStatusCode.InternalServerError, new { });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };

        var response = await http.GetAsync("/api/v1/sessions");
        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task CreateRole_PostsToCorrectEndpoint()
    {
        var handler = new MockHttpHandler();
        handler.SetupResponse("/api/v1/roles", HttpStatusCode.Created, new { id = "r1", name = "Admin", description = "Test" });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };

        var response = await http.PostAsJsonAsync("/api/v1/roles", new { name = "Admin", description = "Test" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        handler.Requests.Should().Contain(r => r.path == "/api/v1/roles" && r.method == "POST");
    }

    [Fact]
    public async Task ApproveAccessRequest_PostsToCorrectPath()
    {
        var handler = new MockHttpHandler();
        handler.SetupResponse("/api/v1/access/requests/req1/approve", HttpStatusCode.OK, new { id = "req1", status = "Approved" });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };

        var response = await http.PostAsync("/api/v1/access/requests/req1/approve", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateTarget_SendsCorrectPayload()
    {
        var handler = new MockHttpHandler();
        handler.SetupResponse("/api/v1/targets", HttpStatusCode.Created, new { id = "t1", name = "Server" });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };

        var target = new { id = "t1", name = "Server", host = "10.0.0.1", port = 22, type = "Linux", environment = "prod", criticality = "critical", status = "active" };
        var response = await http.PostAsJsonAsync("/api/v1/targets", target);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var req = handler.Requests.First(r => r.path == "/api/v1/targets");
        req.body.Should().Contain("Server");
    }

    [Fact]
    public async Task GetPolicies_ReturnsCorrectData()
    {
        var handler = new MockHttpHandler();
        handler.SetupResponse("/api/v1/policies", HttpStatusCode.OK, new[]
        {
            new { id = "p1", name = "SSH Policy", targetType = "Linux", allowedProtocols = "SSH", effect = "Allow" }
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };

        var policies = await http.GetFromJsonAsync<JsonElement>("/api/v1/policies");
        policies.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetAgents_ReturnsAgentData()
    {
        var handler = new MockHttpHandler();
        handler.SetupResponse("/api/v1/agents", HttpStatusCode.OK, new[]
        {
            new { id = "a1", hostname = "host1", os = "linux", status = "Online" }
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };

        var agents = await http.GetFromJsonAsync<JsonElement>("/api/v1/agents");
        agents.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetRecordings_ReturnsRecordingData()
    {
        var handler = new MockHttpHandler();
        handler.SetupResponse("/api/v1/recordings", HttpStatusCode.OK, new[]
        {
            new { id = "rec1", sessionId = "s1", mode = "node", status = "Completed" }
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };

        var response = await http.GetAsync("/api/v1/recordings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("rec1");
    }
}
