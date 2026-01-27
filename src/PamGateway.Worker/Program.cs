using Microsoft.EntityFrameworkCore;
using PamGateway.Core;
using PamGateway.Data;
using PamGateway.Integrations;
using PamGateway.Worker;

var builder = Host.CreateApplicationBuilder(args);

var storageProvider = builder.Configuration.GetValue<string>("Storage:Provider") ?? "Postgres";
if (storageProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("PamGateway");
    builder.Services.AddDbContext<PamGatewayDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddScoped<IAccessRequestStore, EfAccessRequestStore>();
    builder.Services.AddScoped<ITargetStore, EfTargetStore>();
    builder.Services.AddScoped<IAuditStore, EfAuditStore>();
}

builder.Services.Configure<JiraOptions>(builder.Configuration.GetSection("Jira"));
builder.Services.AddHttpClient<IItsmClient, JiraItsmClient>();
builder.Services.AddHostedService<AccessRequestWorker>();

var host = builder.Build();
host.Run();
