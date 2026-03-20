using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Integrations;

namespace PamGateway.Tests.Integration;

public sealed class AccessRequestsApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;
    private readonly PamApiFactory _factory;

    public AccessRequestsApiTests(PamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> EnsureTargetExists(string id = "AR-TARGET")
    {
        var targetType = $"ARType_{id}";
        var dto = new TargetUpsertDto(id, "ARTarget", "10.0.0.1", 22, null, targetType, "prod", "critical", "active");
        await _client.PostAsJsonAsync("/api/v1/targets", dto);
        await _client.PostAsJsonAsync("/api/v1/policies",
            new PolicyCreateDto($"Pol-{targetType}", targetType, "ssh", "Allow", null));
        return id;
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/access/requests");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreated()
    {
        var targetId = await EnsureTargetExists("AR-T-CREATE");
        _factory.ItsmClient
            .CreateAccessRequestAsync(Arg.Any<ItsmAccessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ItsmTicket("PAM-999", "https://jira.test/browse/PAM-999"));

        var dto = new AccessRequestCreateDto(targetId, 60, "need access");
        var response = await _client.PostAsJsonAsync("/api/v1/access/requests", dto);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("PAM-999");
    }

    [Fact]
    public async Task Create_TargetNotFound_Returns404()
    {
        var dto = new AccessRequestCreateDto("NONEXISTENT-TARGET", 60, "reason");
        var response = await _client.PostAsJsonAsync("/api/v1/access/requests", dto);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Approve_PendingRequest()
    {
        var targetId = await EnsureTargetExists("AR-T-APPROVE");
        _factory.ItsmClient
            .CreateAccessRequestAsync(Arg.Any<ItsmAccessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ItsmTicket("PAM-A1", "https://jira.test/browse/PAM-A1"));

        var createResponse = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new AccessRequestCreateDto(targetId, 60, "need access"));
        var created = await createResponse.Content.ReadFromJsonAsync<AccessRequestResponse>();

        var approveResponse = await _client.PostAsync($"/api/v1/access/requests/{created!.Id}/approve", null);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await approveResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Approved");
    }

    [Fact]
    public async Task Deny_PendingRequest()
    {
        var targetId = await EnsureTargetExists("AR-T-DENY");
        _factory.ItsmClient
            .CreateAccessRequestAsync(Arg.Any<ItsmAccessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ItsmTicket("PAM-D1", "https://jira.test/browse/PAM-D1"));

        var createResponse = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new AccessRequestCreateDto(targetId, 60, "need access"));
        var created = await createResponse.Content.ReadFromJsonAsync<AccessRequestResponse>();

        var denyResponse = await _client.PostAsync($"/api/v1/access/requests/{created!.Id}/deny", null);
        denyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await denyResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Denied");
    }

    [Fact]
    public async Task Approve_NotFound_Returns404()
    {
        var response = await _client.PostAsync("/api/v1/access/requests/NONEXISTENT/approve", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record AccessRequestResponse(string Id, string TargetId, string Status);
}
