using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace PamGateway.Tests.Integration;

public sealed class VaultApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public VaultApiTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> CreateCredential(string targetId = "t-vault",
        string username = "admin", bool breakGlass = false)
    {
        var body = JsonSerializer.Serialize(new
        {
            targetId, username, password = "S3cret!Pass",
            isBreakGlass = breakGlass, rotationIntervalHours = 12
        });
        var resp = await _client.PostAsync("/api/v1/vault/credentials",
            new StringContent(body, Encoding.UTF8, "application/json"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task CreateCredential_ReturnsCreated()
    {
        var id = await CreateCredential();
        id.Should().StartWith("CRED-");
    }

    [Fact]
    public async Task GetAll_ReturnsCredentials()
    {
        await CreateCredential();
        var resp = await _client.GetAsync("/api/v1/vault/credentials");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        json.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetById_ReturnsCredential()
    {
        var id = await CreateCredential();
        var resp = await _client.GetAsync($"/api/v1/vault/credentials/{id}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("username").GetString().Should().Be("admin");
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var resp = await _client.GetAsync("/api/v1/vault/credentials/nonexistent");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CheckoutAndCheckin_FullCycle()
    {
        var id = await CreateCredential();

        var checkoutBody = JsonSerializer.Serialize(new { reason = "Maintenance" });
        var coResp = await _client.PostAsync($"/api/v1/vault/credentials/{id}/checkout",
            new StringContent(checkoutBody, Encoding.UTF8, "application/json"));
        coResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var coJson = await JsonDocument.ParseAsync(await coResp.Content.ReadAsStreamAsync());
        coJson.RootElement.GetProperty("password").GetString().Should().Be("S3cret!Pass");
        coJson.RootElement.GetProperty("checkoutId").GetString().Should().StartWith("CO-");

        var ciResp = await _client.PostAsync($"/api/v1/vault/credentials/{id}/checkin", null);
        ciResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResp = await _client.GetAsync($"/api/v1/vault/credentials/{id}");
        var getJson = await JsonDocument.ParseAsync(await getResp.Content.ReadAsStreamAsync());
        getJson.RootElement.GetProperty("status").GetString().Should().Be("Available");
    }

    [Fact]
    public async Task DoubleCheckout_ReturnsConflict()
    {
        var id = await CreateCredential();

        var body = JsonSerializer.Serialize(new { reason = "first" });
        await _client.PostAsync($"/api/v1/vault/credentials/{id}/checkout",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var resp = await _client.PostAsync($"/api/v1/vault/credentials/{id}/checkout",
            new StringContent(body, Encoding.UTF8, "application/json"));
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CheckinWithoutCheckout_ReturnsConflict()
    {
        var id = await CreateCredential();

        var resp = await _client.PostAsync($"/api/v1/vault/credentials/{id}/checkin", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Rotate_ChangesPassword()
    {
        var id = await CreateCredential();

        var rotateResp = await _client.PostAsync($"/api/v1/vault/credentials/{id}/rotate", null);
        rotateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var coBody = JsonSerializer.Serialize(new { reason = "verify rotation" });
        var coResp = await _client.PostAsync($"/api/v1/vault/credentials/{id}/checkout",
            new StringContent(coBody, Encoding.UTF8, "application/json"));
        var coJson = await JsonDocument.ParseAsync(await coResp.Content.ReadAsStreamAsync());
        coJson.RootElement.GetProperty("password").GetString()
            .Should().NotBe("S3cret!Pass", "password should change after rotation");
    }

    [Fact]
    public async Task RotateWhileCheckedOut_ReturnsConflict()
    {
        var id = await CreateCredential();

        var body = JsonSerializer.Serialize(new { reason = "block" });
        await _client.PostAsync($"/api/v1/vault/credentials/{id}/checkout",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var resp = await _client.PostAsync($"/api/v1/vault/credentials/{id}/rotate", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task BreakGlass_CreatesAuditEvent()
    {
        var id = await CreateCredential(breakGlass: true);

        var body = JsonSerializer.Serialize(new { reason = "emergency" });
        await _client.PostAsync($"/api/v1/vault/credentials/{id}/checkout",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var auditResp = await _client.GetAsync("/api/v1/audit/events?limit=1000");
        var auditJson = await JsonDocument.ParseAsync(await auditResp.Content.ReadAsStreamAsync());
        var items = auditJson.RootElement.GetProperty("items").EnumerateArray().ToList();
        items.Should().Contain(e =>
            e.GetProperty("eventType").GetString() == "vault.breakglass.checkout");
    }

    [Fact]
    public async Task GetCheckouts_ReturnsHistory()
    {
        var id = await CreateCredential();

        var body = JsonSerializer.Serialize(new { reason = "test checkout" });
        await _client.PostAsync($"/api/v1/vault/credentials/{id}/checkout",
            new StringContent(body, Encoding.UTF8, "application/json"));
        await _client.PostAsync($"/api/v1/vault/credentials/{id}/checkin", null);

        var resp = await _client.GetAsync($"/api/v1/vault/checkouts?credentialId={id}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        json.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }
}
