using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PamGateway.Core;

namespace PamGateway.Tests.Integration;

public sealed class AuditPaginationTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;
    private readonly PamApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AuditPaginationTests(PamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private Task SeedAuditEvents(int count)
    {
        using var scope = _factory.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditStore>();
        for (var i = 0; i < count; i++)
        {
            audit.Add(new AuditEvent(
                DateTimeOffset.UtcNow.AddMinutes(-count + i),
                "test.event",
                $"user-{i}",
                $"username-{i}",
                "tester",
                "T1",
                "Server",
                "test",
                "success",
                $"REQ-{i}",
                "",
                "127.0.0.1"));
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Get_ReturnsPagedResult_WithTotalAndItems()
    {
        await SeedAuditEvents(5);

        var response = await _client.GetAsync("/api/v1/audit/events?limit=2&offset=0");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(5);
        root.GetProperty("offset").GetInt32().Should().Be(0);
        root.GetProperty("limit").GetInt32().Should().Be(2);
        root.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Get_WithOffset_SkipsEvents()
    {
        await SeedAuditEvents(10);

        var response = await _client.GetAsync("/api/v1/audit/events?limit=3&offset=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("offset").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task Get_DefaultPagination_ReturnsUpTo100()
    {
        var response = await _client.GetAsync("/api/v1/audit/events");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("limit").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task Get_NegativeOffset_ClampsToZero()
    {
        var response = await _client.GetAsync("/api/v1/audit/events?offset=-5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("offset").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Get_LimitOverMax_ClampsTo1000()
    {
        var response = await _client.GetAsync("/api/v1/audit/events?limit=5000");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("limit").GetInt32().Should().Be(1000);
    }
}
