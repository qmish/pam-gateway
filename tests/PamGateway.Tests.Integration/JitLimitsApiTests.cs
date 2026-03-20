using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Integrations;

namespace PamGateway.Tests.Integration;

public sealed class JitLimitsApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;
    private readonly PamApiFactory _factory;

    public JitLimitsApiTests(PamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task EnsureTargetAndPolicy(string targetId, string targetType)
    {
        await _client.PostAsJsonAsync("/api/v1/targets",
            new TargetUpsertDto(targetId, $"JIT Target {targetId}", "10.0.0.1", 22,
                new Dictionary<string, string> { ["os"] = "linux" },
                targetType, "prod", "critical", "active"));

        await _client.PostAsJsonAsync("/api/v1/policies",
            new { Name = $"Pol-{targetId}", TargetType = targetType, AllowedProtocols = "ssh", Effect = "Allow" });
    }

    [Fact]
    public async Task CreateRequest_NoMatchingPolicy_Returns422()
    {
        var targetId = "JIT-NO-POL";
        await _client.PostAsJsonAsync("/api/v1/targets",
            new TargetUpsertDto(targetId, "Orphan Target", "10.0.0.1", 22, null,
                "UniqueTypeNoPolicy_" + Guid.NewGuid().ToString("N"), "prod", "critical", "active"));

        _factory.ItsmClient
            .CreateAccessRequestAsync(Arg.Any<ItsmAccessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ItsmTicket("PAM-JIT-1", "https://jira.test/browse/PAM-JIT-1"));

        var response = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new AccessRequestCreateDto(targetId, 60, "test"));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("No matching policy");
    }

    [Fact]
    public async Task CreateRequest_WithMatchingPolicy_ReturnsCreated()
    {
        var targetId = "JIT-WITH-POL";
        var targetType = "JitTestType_" + Guid.NewGuid().ToString("N")[..8];
        await EnsureTargetAndPolicy(targetId, targetType);

        _factory.ItsmClient
            .CreateAccessRequestAsync(Arg.Any<ItsmAccessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ItsmTicket("PAM-JIT-2", "https://jira.test/browse/PAM-JIT-2"));

        var response = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new AccessRequestCreateDto(targetId, 60, "valid test"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
