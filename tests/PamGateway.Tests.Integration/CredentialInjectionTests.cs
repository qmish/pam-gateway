using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PamGateway.Core;
using PamGateway.Integrations;
using NSubstitute;

namespace PamGateway.Tests.Integration;

public sealed class CredentialInjectionTests : IClassFixture<PamApiFactory>
{
    private readonly PamApiFactory _factory;
    private readonly HttpClient _client;

    public CredentialInjectionTests(PamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        factory.ItsmClient
            .CreateAccessRequestAsync(Arg.Any<ItsmAccessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ItsmTicket("PAM-INJ", "https://jira.test/browse/PAM-INJ"));
    }

    private StringContent Json(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    private void EnsurePolicy(string targetType)
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        if (store.GetAll().Any(p => p.TargetType == targetType)) return;
        store.Add(new Policy($"pol-inj-{targetType}", $"Allow-{targetType}", targetType, "ssh,rdp", "Allow",
            new Dictionary<string, string> { ["env"] = "test" }));
    }

    private async Task<(string targetId, string sessionId)> CreateTargetAndSession(bool withCredential, bool breakGlassOnly = false)
    {
        var targetId = $"tgt-inj-{Guid.NewGuid():N}";
        EnsurePolicy("Linux Server");

        var targetResp = await _client.PostAsync("/api/v1/targets",
            Json(new
            {
                id = targetId, name = "InjTest", host = "10.0.0.1", port = 22,
                labels = new Dictionary<string, string> { ["env"] = "test" },
                type = "Linux Server", environment = "test", criticality = "high", status = "Active"
            }));
        targetResp.StatusCode.Should().Be(HttpStatusCode.Created);

        if (withCredential)
        {
            var credResp = await _client.PostAsync("/api/v1/vault/credentials",
                Json(new
                {
                    targetId, username = "svc-admin", password = "Injected!Pass123",
                    isBreakGlass = breakGlassOnly, rotationIntervalHours = 24
                }));
            credResp.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var reqResp = await _client.PostAsync("/api/v1/access/requests",
            Json(new { targetId, durationMinutes = 60, reason = "inject test" }));
        reqResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var reqJson = await JsonDocument.ParseAsync(await reqResp.Content.ReadAsStreamAsync());
        var requestId = reqJson.RootElement.GetProperty("id").GetString()!;

        var approveResp = await _client.PostAsync($"/api/v1/access/requests/{requestId}/approve", null);
        approveResp.IsSuccessStatusCode.Should().BeTrue();

        var sesResp = await _client.PostAsync("/api/v1/sessions",
            Json(new { targetId, protocol = "ssh", requestId }));
        sesResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var sesJson = await JsonDocument.ParseAsync(await sesResp.Content.ReadAsStreamAsync());
        var sessionId = sesJson.RootElement.GetProperty("id").GetString()!;

        return (targetId, sessionId);
    }

    [Fact]
    public async Task CreateSession_WithCredential_InjectsAutomatically()
    {
        var (_, sessionId) = await CreateTargetAndSession(withCredential: true);

        var resp = await _client.GetAsync($"/api/v1/sessions/{sessionId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("injectedCredentialId").GetString()
            .Should().StartWith("CRED-");
    }

    [Fact]
    public async Task CreateSession_WithoutCredential_NoInjection()
    {
        var (_, sessionId) = await CreateTargetAndSession(withCredential: false);

        var resp = await _client.GetAsync($"/api/v1/sessions/{sessionId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("injectedCredentialId").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task CreateSession_BreakGlassOnly_NotAutoInjected()
    {
        var (_, sessionId) = await CreateTargetAndSession(withCredential: true, breakGlassOnly: true);

        var resp = await _client.GetAsync($"/api/v1/sessions/{sessionId}");
        var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("injectedCredentialId").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetInjectedCredentials_ReturnsPasswordForAgent()
    {
        var (_, sessionId) = await CreateTargetAndSession(withCredential: true);

        var resp = await _client.GetAsync($"/api/v1/sessions/{sessionId}/credentials");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("injected").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("username").GetString().Should().Be("svc-admin");
        json.RootElement.GetProperty("password").GetString().Should().Be("Injected!Pass123");
    }

    [Fact]
    public async Task GetInjectedCredentials_NoCredential_ReturnsFalse()
    {
        var (_, sessionId) = await CreateTargetAndSession(withCredential: false);

        var resp = await _client.GetAsync($"/api/v1/sessions/{sessionId}/credentials");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("injected").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task TerminateSession_AutoCheckinsCredential()
    {
        var (targetId, sessionId) = await CreateTargetAndSession(withCredential: true);

        var sesResp = await _client.GetAsync($"/api/v1/sessions/{sessionId}");
        var sesJson = await JsonDocument.ParseAsync(await sesResp.Content.ReadAsStreamAsync());
        var credId = sesJson.RootElement.GetProperty("injectedCredentialId").GetString()!;

        var credBefore = await _client.GetAsync($"/api/v1/vault/credentials/{credId}");
        var credBeforeJson = await JsonDocument.ParseAsync(await credBefore.Content.ReadAsStreamAsync());
        credBeforeJson.RootElement.GetProperty("status").GetString().Should().Be("CheckedOut");

        await _client.PostAsync($"/api/v1/sessions/{sessionId}/terminate", null);

        var credAfter = await _client.GetAsync($"/api/v1/vault/credentials/{credId}");
        var credAfterJson = await JsonDocument.ParseAsync(await credAfter.Content.ReadAsStreamAsync());
        credAfterJson.RootElement.GetProperty("status").GetString().Should().Be("Available");
    }

    [Fact]
    public async Task GetInjectedCredentials_TerminatedSession_ReturnsConflict()
    {
        var (_, sessionId) = await CreateTargetAndSession(withCredential: true);

        await _client.PostAsync($"/api/v1/sessions/{sessionId}/terminate", null);

        var resp = await _client.GetAsync($"/api/v1/sessions/{sessionId}/credentials");
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CredentialInjection_CreatesAuditEvent()
    {
        var (_, _) = await CreateTargetAndSession(withCredential: true);

        var auditResp = await _client.GetAsync("/api/v1/audit/events?limit=1000");
        var auditJson = await JsonDocument.ParseAsync(await auditResp.Content.ReadAsStreamAsync());
        var items = auditJson.RootElement.GetProperty("items").EnumerateArray().ToList();
        items.Should().Contain(e =>
            e.GetProperty("eventType").GetString() == "vault.credential.injected");
    }
}
