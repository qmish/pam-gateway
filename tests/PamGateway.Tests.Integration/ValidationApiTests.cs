using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PamGateway.Api;

namespace PamGateway.Tests.Integration;

public sealed class ValidationApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public ValidationApiTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("", 60, "reason")]
    [InlineData(null, 60, "reason")]
    public async Task CreateAccessRequest_EmptyTargetId_Returns400(string? targetId, int duration, string reason)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new { TargetId = targetId, DurationMinutes = duration, Reason = reason });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1441)]
    public async Task CreateAccessRequest_InvalidDuration_Returns400(int duration)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new { TargetId = "T1", DurationMinutes = duration, Reason = "ok" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAccessRequest_EmptyReason_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new { TargetId = "T1", DurationMinutes = 60, Reason = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateRole_EmptyName_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/roles",
            new { Name = "", Description = "desc" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("InvalidEffect")]
    [InlineData("")]
    public async Task CreatePolicy_InvalidEffect_Returns400(string effect)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/policies",
            new { Name = "P1", TargetType = "SSH", AllowedProtocols = "ssh", Effect = effect });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePolicy_ValidEffect_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/policies",
            new { Name = "ValidPolicy", TargetType = "SSH", AllowedProtocols = "ssh", Effect = "Allow" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task CreateApproval_InvalidStatus_Returns400(string status)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/approvals",
            new { RequestId = "R1", Approver = "admin", Status = status });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTarget_InvalidPort_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/targets",
            new { Id = "T1", Name = "Test", Type = "SSH", Environment = "prod", Criticality = "high", Status = "active", Port = 0 });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTarget_PortAbove65535_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/targets",
            new { Id = "T1", Name = "Test", Type = "SSH", Environment = "prod", Criticality = "high", Status = "active", Port = 70000 });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateRecording_InvalidMode_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/recordings",
            new { SessionId = "S1", Mode = "invalid-mode" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSession_EmptyProtocol_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/sessions",
            new { TargetId = "T1", Protocol = "", RequestId = "R1" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BadRequest_ReturnsProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new { TargetId = "", DurationMinutes = 0, Reason = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("errors");
    }
}
