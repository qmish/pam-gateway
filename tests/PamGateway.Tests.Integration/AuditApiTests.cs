using System.Net;
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
    public async Task GetEvents_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/audit/events");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEvents_WithFilters_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/audit/events?user=testuser&from=2026-01-01T00:00:00Z");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
