using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PamGateway.Api;

namespace PamGateway.Tests.Integration;

public sealed class AgentsApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public AgentsApiTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/agents");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_ReturnsToken()
    {
        var dto = new AgentRegisterDto(null, $"agent-int-{Guid.NewGuid():N}", "host1", "linux", "http://agent:7071",
            new Dictionary<string, string> { ["env"] = "test" }, new List<string> { "ssh" });

        var response = await _client.PostAsJsonAsync("/api/v1/agents/register", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("agentToken");
    }

    [Fact]
    public async Task Heartbeat_RegisteredAgent_ReturnsOk()
    {
        var agentId = $"agent-hb-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/v1/agents/register",
            new AgentRegisterDto(null, agentId, "host1", "linux", "http://agent:7071",
                new Dictionary<string, string>(), new List<string>()));

        var hbDto = new AgentHeartbeatDto(agentId, "ok", 0, new Dictionary<string, string>());
        var response = await _client.PostAsJsonAsync("/api/v1/agents/heartbeat", hbDto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Heartbeat_UnknownAgent_ReturnsNotFound()
    {
        var dto = new AgentHeartbeatDto("nonexistent-agent", "ok", 0, new Dictionary<string, string>());
        var response = await _client.PostAsJsonAsync("/api/v1/agents/heartbeat", dto);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
