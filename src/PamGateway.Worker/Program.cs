using Microsoft.EntityFrameworkCore;
using PamGateway.Core;
using PamGateway.Data;
using PamGateway.Integrations;
using PamGateway.Worker;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);
var observability = builder.Configuration.GetSection("Observability").Get<ObservabilityOptions>() ?? new ObservabilityOptions();
if (observability.Enabled)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("pam-gateway-worker"))
        .WithTracing(tracing =>
        {
            tracing.AddSource("PamGateway.Worker");
            tracing.AddHttpClientInstrumentation();
            if (!string.IsNullOrWhiteSpace(observability.OtlpEndpoint))
            {
                tracing.AddOtlpExporter(options => options.Endpoint = new Uri(observability.OtlpEndpoint));
            }
        })
        .WithMetrics(metrics =>
        {
            if (!string.IsNullOrWhiteSpace(observability.OtlpEndpoint))
            {
                metrics.AddOtlpExporter(options => options.Endpoint = new Uri(observability.OtlpEndpoint));
            }
        });
}

var storageProvider = builder.Configuration.GetValue<string>("Storage:Provider") ?? "Postgres";
if (storageProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("PamGateway");
    builder.Services.AddDbContext<PamGatewayDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddScoped<IAccessRequestStore, EfAccessRequestStore>();
    builder.Services.AddScoped<ISessionStore, EfSessionStore>();
    builder.Services.AddScoped<ITargetStore, EfTargetStore>();
    builder.Services.AddScoped<IAuditStore, EfAuditStore>();
    builder.Services.AddScoped<IAgentStore, EfAgentStore>();
    builder.Services.AddScoped<IAgentTicketStore, EfAgentTicketStore>();
}
else if (storageProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("PamGateway");
    builder.Services.AddDbContext<PamGatewayDbContext>(options => options.UseSqlServer(connectionString));
    builder.Services.AddScoped<IAccessRequestStore, EfAccessRequestStore>();
    builder.Services.AddScoped<ISessionStore, EfSessionStore>();
    builder.Services.AddScoped<ITargetStore, EfTargetStore>();
    builder.Services.AddScoped<IAuditStore, EfAuditStore>();
    builder.Services.AddScoped<IAgentStore, EfAgentStore>();
    builder.Services.AddScoped<IAgentTicketStore, EfAgentTicketStore>();
}

builder.Services.Configure<JiraOptions>(builder.Configuration.GetSection("Jira"));
builder.Services.Configure<SlaOptions>(builder.Configuration.GetSection("Sla"));
builder.Services.AddHttpClient<IItsmClient, JiraItsmClient>();
builder.Services.AddSingleton<IDeadLetterStore, InMemoryDeadLetterStore>();
builder.Services.AddHostedService<AccessRequestWorker>();
builder.Services.AddHostedService<DeadLetterProcessor>();

builder.Services.AddSingleton<WorkerHealthState>();

var host = builder.Build();
host.Run();
