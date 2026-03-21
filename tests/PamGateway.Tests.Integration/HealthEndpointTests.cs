using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace PamGateway.Tests.Integration;

public sealed class HealthEndpointTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("status").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task Liveness_ReturnsAlive()
    {
        var response = await _client.GetAsync("/api/v1/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("status").GetString().Should().Be("alive");
    }

    [Fact]
    public async Task Readiness_ReturnsReadyWithChecks()
    {
        var response = await _client.GetAsync("/api/v1/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("status").GetString().Should().Be("ready");

        var checks = json.RootElement.GetProperty("checks");
        checks.GetArrayLength().Should().BeGreaterThanOrEqualTo(4);

        foreach (var check in checks.EnumerateArray())
        {
            check.GetProperty("status").GetString().Should().Be("ok");
        }
    }

    [Fact]
    public async Task Readiness_ContainsAllStoreChecks()
    {
        var response = await _client.GetAsync("/api/v1/health/ready");
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var checks = json.RootElement.GetProperty("checks");

        var names = checks.EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToList();

        names.Should().Contain("targets");
        names.Should().Contain("requests");
        names.Should().Contain("sessions");
        names.Should().Contain("agents");
    }
}
