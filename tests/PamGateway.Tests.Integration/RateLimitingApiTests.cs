using System.Net;
using FluentAssertions;

namespace PamGateway.Tests.Integration;

public sealed class RateLimitingApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public RateLimitingApiTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Api_ReturnsOk_UnderNormalLoad()
    {
        var response = await _client.GetAsync("/api/v1/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Api_TargetsEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/targets");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Api_MultipleRequests_SucceedUnderLimit()
    {
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _client.GetAsync("/api/v1/health"))
            .ToArray();
        var responses = await Task.WhenAll(tasks);
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
    }
}
