using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace PamGateway.Tests.Integration;

public sealed class HealthProbeTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public HealthProbeTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Liveness_ReturnsAlive()
    {
        var response = await _client.GetAsync("/api/v1/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("alive");
    }

    [Fact]
    public async Task Readiness_ReturnsReady()
    {
        var response = await _client.GetAsync("/api/v1/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ready");
    }

    [Fact]
    public async Task Readiness_ContainsChecks()
    {
        var response = await _client.GetAsync("/api/v1/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("checks").GetArrayLength().Should().BeGreaterThanOrEqualTo(4);
    }
}
