using System.Net;
using FluentAssertions;

namespace PamGateway.Tests.Integration;

public sealed class HealthApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public HealthApiTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
