using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace PamGateway.Tests.Unit;

public sealed class StubCmdbClientTests
{
    [Fact]
    public async Task FetchTargetsAsync_EmptyConfig_ReturnsEmpty()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var client = new PamGateway.Api.StubCmdbClient(config);
        var targets = await client.FetchTargetsAsync(CancellationToken.None);

        targets.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchTargetsAsync_NoTargetsSection_ReturnsEmpty()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SomeOtherKey"] = "value"
            })
            .Build();

        var client = new PamGateway.Api.StubCmdbClient(config);
        var targets = await client.FetchTargetsAsync(CancellationToken.None);

        targets.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchTargetsAsync_ReturnsSameResultOnMultipleCalls()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var client = new PamGateway.Api.StubCmdbClient(config);

        var first = await client.FetchTargetsAsync(CancellationToken.None);
        var second = await client.FetchTargetsAsync(CancellationToken.None);

        first.Should().BeSameAs(second);
    }

    [Fact]
    public async Task FetchTargetsAsync_IsIdempotent()
    {
        var config = new ConfigurationBuilder().Build();
        var client = new PamGateway.Api.StubCmdbClient(config);

        var result1 = await client.FetchTargetsAsync(CancellationToken.None);
        var result2 = await client.FetchTargetsAsync(CancellationToken.None);

        result1.Should().BeEquivalentTo(result2);
    }
}
