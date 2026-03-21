using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace PamGateway.Tests.Integration;

public sealed class ObservabilityIntegrationTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public ObservabilityIntegrationTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ok");
    }

    [Fact]
    public async Task LivenessEndpoint_ReturnsAlive()
    {
        var response = await _client.GetAsync("/api/v1/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("alive");
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsReady()
    {
        var response = await _client.GetAsync("/api/v1/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("status").GetString().Should().Be("ready");

        var checks = json.RootElement.GetProperty("checks");
        checks.GetArrayLength().Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task ReadinessEndpoint_ContainsStoreChecks()
    {
        var response = await _client.GetAsync("/api/v1/health/ready");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var checks = json.RootElement.GetProperty("checks");

        var names = new List<string>();
        foreach (var check in checks.EnumerateArray())
        {
            names.Add(check.GetProperty("name").GetString()!);
            check.GetProperty("status").GetString().Should().Be("ok");
        }

        names.Should().Contain("targets");
        names.Should().Contain("requests");
        names.Should().Contain("sessions");
        names.Should().Contain("agents");
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsJsonContentType()
    {
        var response = await _client.GetAsync("/api/v1/health");

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task MultipleHealthChecks_AreConsistent()
    {
        for (int i = 0; i < 5; i++)
        {
            var response = await _client.GetAsync("/api/v1/health/ready");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
