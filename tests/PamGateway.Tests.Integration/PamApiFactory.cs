using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PamGateway.Api;
using PamGateway.Core;
using PamGateway.Data;
using PamGateway.Integrations;
using NSubstitute;

namespace PamGateway.Tests.Integration;

public sealed class PamApiFactory : WebApplicationFactory<Program>
{
    public IItsmClient ItsmClient { get; } = Substitute.For<IItsmClient>();
    public ICmdbClient CmdbClient { get; } = Substitute.For<ICmdbClient>();

    private int? _rateLimitPermit;
    private int? _rateLimitWindow;

    public void WithRateLimitConfig(int permitLimit, int windowSeconds)
    {
        _rateLimitPermit = permitLimit;
        _rateLimitWindow = windowSeconds;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Auth:Enabled"] = "false",
                ["Storage:Provider"] = "InMemory",
                ["Cmdb:Provider"] = "Stub",
                ["DemoData:Enabled"] = "false",
                ["DemoData:SeedIfEmpty"] = "false",
                ["Observability:Enabled"] = "false",
                ["Jira:BaseUrl"] = "https://jira.test",
                ["Jira:ProjectKey"] = "PAM",
                ["Agent:RequireAgentToken"] = "false",
                ["Agent:JoinToken"] = "",
                ["Jit:MaxActiveRequestsPerUser"] = "100",
                ["CmdbSync:Enabled"] = "false",
                ["SiemExport:Enabled"] = "false",
                ["RecordingStorage:Provider"] = "Local",
                ["RecordingStorage:LocalPath"] = Path.Combine(Path.GetTempPath(), "pam-test-recordings"),
                ["ConnectionStrings:PamGateway"] = ""
            };

            if (_rateLimitPermit.HasValue)
            {
                settings["RateLimiting:Api:PermitLimit"] = _rateLimitPermit.Value.ToString();
                settings["RateLimiting:Api:WindowSeconds"] = (_rateLimitWindow ?? 60).ToString();
            }

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<PamGatewayDbContext>));
            if (dbDescriptor != null) services.Remove(dbDescriptor);

            services.RemoveAll<PamGatewayDbContext>();
            services.RemoveAll<DbContextOptions<PamGatewayDbContext>>();

            services.RemoveAll<IAccessRequestStore>();
            services.RemoveAll<ISessionStore>();
            services.RemoveAll<IRecordingStore>();
            services.RemoveAll<ITargetStore>();
            services.RemoveAll<IAuditStore>();
            services.RemoveAll<IRoleStore>();
            services.RemoveAll<IPolicyStore>();
            services.RemoveAll<IApprovalStore>();
            services.RemoveAll<IAgentStore>();
            services.RemoveAll<IAgentTicketStore>();

            services.AddSingleton<IAccessRequestStore, InMemoryAccessRequestStore>();
            services.AddSingleton<ISessionStore, InMemorySessionStore>();
            services.AddSingleton<IRecordingStore, InMemoryRecordingStore>();
            services.AddSingleton<ITargetStore, InMemoryTargetStore>();
            services.AddSingleton<IAuditStore, InMemoryAuditStore>();
            services.AddSingleton<IRoleStore, InMemoryRoleStore>();
            services.AddSingleton<IPolicyStore, InMemoryPolicyStore>();
            services.AddSingleton<IApprovalStore, InMemoryApprovalStore>();
            services.AddSingleton<IAgentStore, InMemoryAgentStore>();
            services.AddSingleton<IAgentTicketStore, InMemoryAgentTicketStore>();

            services.RemoveAll<IItsmClient>();
            services.AddSingleton(ItsmClient);

            services.RemoveAll<ICmdbClient>();
            services.AddSingleton(CmdbClient);

            services.AddAuthorization(options =>
            {
                var allowAll = new AuthorizationPolicyBuilder()
                    .RequireAssertion(_ => true)
                    .Build();
                options.DefaultPolicy = allowAll;
                options.FallbackPolicy = allowAll;
            });

            services.AddSingleton<IAuthorizationHandler, AllowAllHandler>();
        });
    }

    private sealed class AllowAllHandler : IAuthorizationHandler
    {
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.PendingRequirements.ToList())
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }
    }
}
