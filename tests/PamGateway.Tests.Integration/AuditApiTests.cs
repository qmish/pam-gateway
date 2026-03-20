using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace PamGateway.Tests.Integration;

public sealed class AuditApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public AuditApiTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetEvents_ReturnsOk_WithPaginatedFormat()
    {
        var response = await _client.GetAsync("/api/v1/audit/events");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("total", out _).Should().BeTrue();
        root.TryGetProperty("offset", out _).Should().BeTrue();
        root.TryGetProperty("limit", out _).Should().BeTrue();
        root.TryGetProperty("items", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetEvents_WithFilters_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/audit/events?user=testuser&from=2026-01-01T00:00:00Z");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
