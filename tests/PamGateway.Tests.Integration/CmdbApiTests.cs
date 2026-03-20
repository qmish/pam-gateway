using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using PamGateway.Integrations;

namespace PamGateway.Tests.Integration;

public sealed class CmdbApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;
    private readonly PamApiFactory _factory;

    public CmdbApiTests(PamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Sync_ReturnsImportedCount()
    {
        var cmdbTargets = new List<CmdbTarget>
        {
            new("CMDB-1", "Server A", "SSH", "prod", "critical", "Active"),
            new("CMDB-2", "Server B", "RDP", "test", "non-critical", "Active")
        };

        _factory.CmdbClient
            .FetchTargetsAsync(Arg.Any<CancellationToken>())
            .Returns(cmdbTargets);

        var response = await _client.PostAsync("/api/v1/cmdb/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SyncResult>();
        result!.Imported.Should().Be(2);
    }

    [Fact]
    public async Task Sync_EmptyCmdb_ReturnsZero()
    {
        _factory.CmdbClient
            .FetchTargetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>());

        var response = await _client.PostAsync("/api/v1/cmdb/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SyncResult>();
        result!.Imported.Should().Be(0);
    }

    [Fact]
    public async Task Sync_ImportsTargets_ThenVisibleInTargetsApi()
    {
        var cmdbTargets = new List<CmdbTarget>
        {
            new("CMDB-VISIBLE", "Visible Server", "SSH", "prod", "critical", "Active")
        };

        _factory.CmdbClient
            .FetchTargetsAsync(Arg.Any<CancellationToken>())
            .Returns(cmdbTargets);

        await _client.PostAsync("/api/v1/cmdb/sync", null);

        var targetsResponse = await _client.GetAsync("/api/v1/targets");
        var body = await targetsResponse.Content.ReadAsStringAsync();
        body.Should().Contain("CMDB-VISIBLE");
    }

    private sealed record SyncResult(int Imported);
}
