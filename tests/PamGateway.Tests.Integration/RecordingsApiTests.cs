using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Integrations;

namespace PamGateway.Tests.Integration;

public sealed class RecordingsApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;
    private readonly PamApiFactory _factory;

    public RecordingsApiTests(PamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> CreateActiveSession()
    {
        var targetId = $"REC-T-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/v1/targets",
            new TargetUpsertDto(targetId, "RecTarget", "10.0.0.10", 22, null, "Linux", "prod", "critical", "active"));

        _factory.ItsmClient
            .CreateAccessRequestAsync(Arg.Any<ItsmAccessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ItsmTicket("PAM-R1", "url"));

        var reqResp = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new AccessRequestCreateDto(targetId, 60, "rec test"));
        var req = await reqResp.Content.ReadFromJsonAsync<IdDto>();
        await _client.PostAsync($"/api/v1/access/requests/{req!.Id}/approve", null);

        var sesResp = await _client.PostAsJsonAsync("/api/v1/sessions",
            new SessionCreateDto(targetId, "ssh", req.Id));
        var ses = await sesResp.Content.ReadFromJsonAsync<IdDto>();
        return ses!.Id;
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/recordings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_ValidRecording()
    {
        var sessionId = await CreateActiveSession();
        var dto = new RecordingCreateDto(sessionId, "node", null);
        var response = await _client.PostAsJsonAsync("/api/v1/recordings", dto);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_InvalidMode_ReturnsBadRequest()
    {
        var sessionId = await CreateActiveSession();
        var dto = new RecordingCreateDto(sessionId, "invalid_mode", null);
        var response = await _client.PostAsJsonAsync("/api/v1/recordings", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_SessionNotFound_Returns404()
    {
        var dto = new RecordingCreateDto("NONEXISTENT-SESSION", "node", null);
        var response = await _client.PostAsJsonAsync("/api/v1/recordings", dto);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record IdDto(string Id);
}
