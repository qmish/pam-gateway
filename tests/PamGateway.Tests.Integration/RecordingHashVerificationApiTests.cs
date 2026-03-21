using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Tests.Integration;

public sealed class RecordingHashVerificationApiTests : IClassFixture<PamApiFactory>
{
    private readonly PamApiFactory _factory;
    private readonly HttpClient _client;

    public RecordingHashVerificationApiTests(PamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        factory.ItsmClient
            .CreateAccessRequestAsync(Arg.Any<ItsmAccessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ItsmTicket("PAM-HASH", "https://jira.test/browse/PAM-HASH"));

        EnsurePolicy("Linux");
    }

    private void EnsurePolicy(string targetType)
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        var id = $"hash-test-{targetType}";
        if (store.GetById(id) is null)
        {
            store.Add(new Policy(id, $"Allow{targetType}", targetType, "*", "Allow",
                new Dictionary<string, string>()));
        }
    }

    private async Task<string> SeedTargetAndSession()
    {
        var targetId = $"t-hash-{Guid.NewGuid():N}";
        var targetBody = JsonSerializer.Serialize(new
        {
            id = targetId,
            name = "HashTestTarget",
            host = "10.0.0.99",
            port = 22,
            type = "Linux",
            environment = "test",
            criticality = "non-critical",
            status = "Active"
        });
        await _client.PostAsync("/api/v1/targets",
            new StringContent(targetBody, Encoding.UTF8, "application/json"));

        var reqBody = JsonSerializer.Serialize(new
        {
            targetId,
            durationMinutes = 60,
            reason = "recording hash test"
        });
        var reqResp = await _client.PostAsync("/api/v1/access/requests",
            new StringContent(reqBody, Encoding.UTF8, "application/json"));
        reqResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var reqJson = await JsonDocument.ParseAsync(await reqResp.Content.ReadAsStreamAsync());
        var requestId = reqJson.RootElement.GetProperty("id").GetString()!;

        await _client.PostAsync($"/api/v1/access/requests/{requestId}/approve", null);

        var sessionBody = JsonSerializer.Serialize(new
        {
            targetId,
            requestId,
            protocol = "ssh"
        });
        var sessionResp = await _client.PostAsync("/api/v1/sessions",
            new StringContent(sessionBody, Encoding.UTF8, "application/json"));
        sessionResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var sessionJson = await JsonDocument.ParseAsync(await sessionResp.Content.ReadAsStreamAsync());
        return sessionJson.RootElement.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task UploadAndDownload_WithHashVerify_ReturnsContent()
    {
        var sessionId = await SeedTargetAndSession();

        var createBody = JsonSerializer.Serialize(new { sessionId, mode = "node" });
        var createResp = await _client.PostAsync("/api/v1/recordings",
            new StringContent(createBody, Encoding.UTF8, "application/json"));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var recJson = await JsonDocument.ParseAsync(await createResp.Content.ReadAsStreamAsync());
        var recId = recJson.RootElement.GetProperty("id").GetString()!;

        var content = Encoding.UTF8.GetBytes("Test recording payload for hash verification");
        var uploadResp = await _client.PostAsync($"/api/v1/recordings/{recId}/content",
            new ByteArrayContent(content));
        uploadResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadJson = await JsonDocument.ParseAsync(await uploadResp.Content.ReadAsStreamAsync());
        uploadJson.RootElement.GetProperty("hash").GetString().Should().NotBeNullOrWhiteSpace();

        var downloadResp = await _client.GetAsync($"/api/v1/recordings/{recId}/content?verify=true");
        downloadResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloaded = await downloadResp.Content.ReadAsByteArrayAsync();
        downloaded.Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task ChunkedUploadAndFinalize_VerifiesHash()
    {
        var sessionId = await SeedTargetAndSession();

        var createBody = JsonSerializer.Serialize(new { sessionId, mode = "node" });
        var createResp = await _client.PostAsync("/api/v1/recordings",
            new StringContent(createBody, Encoding.UTF8, "application/json"));
        var recJson = await JsonDocument.ParseAsync(await createResp.Content.ReadAsStreamAsync());
        var recId = recJson.RootElement.GetProperty("id").GetString()!;

        var chunk0 = Encoding.UTF8.GetBytes("First chunk. ");
        var chunk1 = Encoding.UTF8.GetBytes("Second chunk.");

        var c0 = await _client.PutAsync($"/api/v1/recordings/{recId}/chunks/0", new ByteArrayContent(chunk0));
        c0.StatusCode.Should().Be(HttpStatusCode.OK);

        var c1 = await _client.PutAsync($"/api/v1/recordings/{recId}/chunks/1", new ByteArrayContent(chunk1));
        c1.StatusCode.Should().Be(HttpStatusCode.OK);

        var finalizeResp = await _client.PostAsync($"/api/v1/recordings/{recId}/chunks/finalize?totalChunks=2", null);
        finalizeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalJson = await JsonDocument.ParseAsync(await finalizeResp.Content.ReadAsStreamAsync());
        finalJson.RootElement.GetProperty("hash").GetString().Should().NotBeNullOrWhiteSpace();
        finalJson.RootElement.GetProperty("status").GetString().Should().Be("Completed");

        var downloadResp = await _client.GetAsync($"/api/v1/recordings/{recId}/content?verify=true");
        downloadResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloaded = await downloadResp.Content.ReadAsByteArrayAsync();
        Encoding.UTF8.GetString(downloaded).Should().Be("First chunk. Second chunk.");
    }

    [Fact]
    public async Task Download_WithoutContent_ReturnsConflict()
    {
        var sessionId = await SeedTargetAndSession();

        var createBody = JsonSerializer.Serialize(new { sessionId, mode = "node" });
        var createResp = await _client.PostAsync("/api/v1/recordings",
            new StringContent(createBody, Encoding.UTF8, "application/json"));
        var recJson = await JsonDocument.ParseAsync(await createResp.Content.ReadAsStreamAsync());
        var recId = recJson.RootElement.GetProperty("id").GetString()!;

        var downloadResp = await _client.GetAsync($"/api/v1/recordings/{recId}/content");
        downloadResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
