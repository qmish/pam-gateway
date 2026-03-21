using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PamGateway.Tests.Integration;

public sealed class PamUiFactory : WebApplicationFactory<PamGateway.Ui.Pages.IndexModel>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:BaseUrl"] = "http://fake-api"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddHttpClient<PamGateway.Ui.ApiClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new FakeApiHandler());
        });
    }

    private sealed class FakeApiHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            object body = path switch
            {
                "/api/v1/targets" => new[]
                {
                    new { Id = "t1", Name = "Server-1", Host = "10.0.0.1", Port = 22,
                          Labels = new Dictionary<string, string> { ["env"] = "prod" },
                          Type = "Linux Server", Environment = "prod", Criticality = "critical", Status = "Active" }
                },
                "/api/v1/agents" => new[]
                {
                    new { Id = "a1", Hostname = "agent-01", Os = "linux", Status = "Online",
                          LastSeenAt = DateTimeOffset.UtcNow, PublicUrl = "https://agent-01:8443",
                          Labels = new Dictionary<string, string>(), Capabilities = new List<string> { "ssh" } }
                },
                "/api/v1/sessions" => new[]
                {
                    new { Id = "S1", TargetId = "t1", RequestId = "r1", Protocol = "ssh",
                          Status = "Active", StartedAt = DateTimeOffset.UtcNow, EndedAt = (DateTimeOffset?)null }
                },
                "/api/v1/recordings" => new[]
                {
                    new { Id = "REC-1", SessionId = "S1", Mode = "node", StorageUri = "file:///tmp/r.bin",
                          Status = "Completed", StartedAt = DateTimeOffset.UtcNow,
                          EndedAt = (DateTimeOffset?)DateTimeOffset.UtcNow, SizeBytes = (long?)1024, Hash = "ABC123" }
                },
                "/api/v1/access/requests" => new[]
                {
                    new { Id = "REQ-1", TargetId = "t1", RequestedBy = "user1", DurationMinutes = 60,
                          Reason = "Maintenance", Status = "Pending",
                          CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                          ItsmKey = "PAM-1" }
                },
                "/api/v1/approvals" => new[]
                {
                    new { Id = "APR-1", RequestId = "REQ-1", Approver = "admin", ApprovedAt = DateTimeOffset.UtcNow, Status = "approved" }
                },
                "/api/v1/policies" => new[]
                {
                    new { Id = "p1", Name = "AllowSSH", TargetType = "Linux Server", AllowedProtocols = "ssh",
                          Effect = "Allow", TargetLabelSelector = new Dictionary<string, string> { ["env"] = "prod" } }
                },
                "/api/v1/roles" => new[]
                {
                    new { Id = "r1", Name = "System_Admin_Linux", Description = "Linux administrators" }
                },
                _ => new { status = "ok" }
            };

            var json = JsonSerializer.Serialize(body);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
