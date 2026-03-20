using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PamGateway.Api;

namespace PamGateway.Tests.Integration;

public sealed class RolesApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public RolesApiTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/roles");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_ReturnsCreated()
    {
        var dto = new RoleCreateDto("TestRole", "A test role");
        var response = await _client.PostAsJsonAsync("/api/v1/roles", dto);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("TestRole");
    }
}

public sealed class PoliciesApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public PoliciesApiTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/policies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_ReturnsCreated()
    {
        var dto = new PolicyCreateDto("TestPolicy", "Linux", "ssh", "allow", null);
        var response = await _client.PostAsJsonAsync("/api/v1/policies", dto);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Update_ExistingPolicy()
    {
        var createDto = new PolicyCreateDto("UpdateMe", "Linux", "ssh", "allow", null);
        var createResp = await _client.PostAsJsonAsync("/api/v1/policies", createDto);
        var created = await createResp.Content.ReadFromJsonAsync<PolicyDto>();

        var updateDto = new PolicyUpsertDto(created!.Id, "UpdateMe", "Windows", "rdp", "deny", null);
        var response = await _client.PutAsJsonAsync($"/api/v1/policies/{created.Id}", updateDto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record PolicyDto(string Id, string Name);
}
