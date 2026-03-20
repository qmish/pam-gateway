using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PamGateway.Api;

namespace PamGateway.Tests.Integration;

public sealed class TargetsApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public TargetsApiTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/targets");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_And_GetById()
    {
        var dto = new TargetUpsertDto("INT-T1", "IntegrationTarget", "10.0.0.1", 22, null, "Linux", "prod", "critical", "active");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/targets", dto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await _client.GetAsync("/api/v1/targets/INT-T1");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getResponse.Content.ReadAsStringAsync();
        body.Should().Contain("IntegrationTarget");
    }

    [Fact]
    public async Task Update_ExistingTarget()
    {
        var dto = new TargetUpsertDto("INT-T-UPD", "Original", "10.0.0.2", 3389, null, "Windows", "prod", "critical", "active");
        await _client.PostAsJsonAsync("/api/v1/targets", dto);

        var updated = dto with { Name = "Updated" };
        var response = await _client.PutAsJsonAsync("/api/v1/targets/INT-T-UPD", updated);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await (await _client.GetAsync("/api/v1/targets/INT-T-UPD")).Content.ReadAsStringAsync();
        body.Should().Contain("Updated");
    }

    [Fact]
    public async Task Update_IdMismatch_ReturnsBadRequest()
    {
        var dto = new TargetUpsertDto("DIFFERENT", "Name", null, null, null, "Linux", "prod", "critical", "active");
        var response = await _client.PutAsJsonAsync("/api/v1/targets/INT-T-MISMATCH", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_NotFound()
    {
        var response = await _client.GetAsync("/api/v1/targets/NONEXISTENT");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
