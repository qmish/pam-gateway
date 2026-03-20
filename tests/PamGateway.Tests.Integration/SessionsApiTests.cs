using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Integrations;

namespace PamGateway.Tests.Integration;

public sealed class SessionsApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;
    private readonly PamApiFactory _factory;

    public SessionsApiTests(PamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> CreateApprovedRequest()
    {
        var targetId = $"S-T-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/v1/targets",
            new TargetUpsertDto(targetId, "SessionTarget", "10.0.0.5", 22, null, "Linux", "prod", "critical", "active"));

        _factory.ItsmClient
            .CreateAccessRequestAsync(Arg.Any<ItsmAccessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ItsmTicket("PAM-S1", "url"));

        var createReqResp = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new AccessRequestCreateDto(targetId, 60, "session test"));
        var request = await createReqResp.Content.ReadFromJsonAsync<RequestDto>();
        await _client.PostAsync($"/api/v1/access/requests/{request!.Id}/approve", null);
        return request.Id;
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/sessions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_WithApprovedRequest_ReturnsCreated()
    {
        var requestId = await CreateApprovedRequest();
        var targetId = (await (await _client.GetAsync($"/api/v1/access/requests/{requestId}"))
            .Content.ReadFromJsonAsync<RequestDto>())!.TargetId;

        var dto = new SessionCreateDto(targetId, "ssh", requestId);
        var response = await _client.PostAsJsonAsync("/api/v1/sessions", dto);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_WithoutApprovedRequest_ReturnsConflict()
    {
        var dto = new SessionCreateDto("T-FAKE", "ssh", "REQ-NONEXISTENT");
        var response = await _client.PostAsJsonAsync("/api/v1/sessions", dto);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Terminate_ActiveSession()
    {
        var requestId = await CreateApprovedRequest();
        var req = await (await _client.GetAsync($"/api/v1/access/requests/{requestId}"))
            .Content.ReadFromJsonAsync<RequestDto>();

        var sessionResp = await _client.PostAsJsonAsync("/api/v1/sessions",
            new SessionCreateDto(req!.TargetId, "ssh", requestId));
        var session = await sessionResp.Content.ReadFromJsonAsync<SessionDto>();

        var terminateResp = await _client.PostAsync($"/api/v1/sessions/{session!.Id}/terminate", null);
        terminateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await terminateResp.Content.ReadAsStringAsync();
        body.Should().Contain("Terminated");
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/sessions/NONEXISTENT");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record RequestDto(string Id, string TargetId, string Status);
    private sealed record SessionDto(string Id, string Status);
}
