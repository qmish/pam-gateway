using System.Net;
using FluentAssertions;

namespace PamGateway.Tests.Integration;

public sealed class AuditImmutabilityTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public AuditImmutabilityTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AuditEvents_Get_IsAllowed()
    {
        var response = await _client.GetAsync("/api/v1/audit/events");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuditEvents_Delete_IsBlocked()
    {
        var response = await _client.DeleteAsync("/api/v1/audit/events");
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task AuditEvents_Put_IsBlocked()
    {
        var response = await _client.PutAsync("/api/v1/audit/events",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task AuditEvents_Patch_IsBlocked()
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/audit/events")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task AuditEventsWithId_Delete_IsBlocked()
    {
        var response = await _client.DeleteAsync("/api/v1/audit/events/some-id");
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }
}
