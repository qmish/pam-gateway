using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.WebSockets;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using PamGateway.Api;
using PamGateway.Core;
using PamGateway.Data;
using PamGateway.Integrations;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);
var authEnabled = builder.Configuration.GetValue<bool?>("Auth:Enabled") ?? true;

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddMemoryCache();
builder.Services.Configure<AccessOptions>(builder.Configuration.GetSection("Access"));
builder.Services.Configure<AgentApiOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.Configure<AuthRoleMappingOptions>(builder.Configuration.GetSection("Auth:RoleMapping"));
builder.Services.Configure<RecordingOptions>(builder.Configuration.GetSection("Recording"));
builder.Services.Configure<RecordingStorageOptions>(builder.Configuration.GetSection("RecordingStorage"));
builder.Services.Configure<DemoDataOptions>(builder.Configuration.GetSection("DemoData"));
builder.Services.Configure<JitOptions>(builder.Configuration.GetSection("Jit"));
builder.Services.Configure<PamGateway.Api.Services.CmdbSyncOptions>(builder.Configuration.GetSection("CmdbSync"));
builder.Services.Configure<PamGateway.Api.Services.SiemExportOptions>(builder.Configuration.GetSection("SiemExport"));
builder.Services.AddSingleton<PamGateway.Api.Services.SystemDataSeeder>();
builder.Services.AddScoped<AccessPolicyEvaluator>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 20);
        limiter.Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60));
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = builder.Configuration.GetValue("RateLimiting:Api:PermitLimit", 100);
        limiter.Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:Api:WindowSeconds", 60));
        limiter.QueueLimit = 0;
    });
});
var observability = builder.Configuration.GetSection("Observability").Get<ObservabilityOptions>() ?? new ObservabilityOptions();
if (observability.Enabled)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("pam-gateway-api"))
        .WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation();
            tracing.AddHttpClientInstrumentation();
            if (!string.IsNullOrWhiteSpace(observability.OtlpEndpoint))
            {
                tracing.AddOtlpExporter(options => options.Endpoint = new Uri(observability.OtlpEndpoint));
            }
        })
        .WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation();
            if (!string.IsNullOrWhiteSpace(observability.OtlpEndpoint))
            {
                metrics.AddOtlpExporter(options => options.Endpoint = new Uri(observability.OtlpEndpoint));
            }
        });
}
if (authEnabled)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Auth:Authority"];
            options.Audience = builder.Configuration["Auth:Audience"];
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                RoleClaimType = "roles"
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddSingleton<IClaimsTransformation, KeycloakRoleClaimsTransformation>();
}
else
{
    builder.Services.AddAuthorization(options =>
    {
        var allowAll = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
        options.DefaultPolicy = allowAll;
        options.FallbackPolicy = allowAll;
    });
    builder.Services.AddSingleton<IAuthorizationHandler, AllowAllRolesHandler>();
}

builder.Services.Configure<JiraOptions>(builder.Configuration.GetSection("Jira"));
builder.Services.AddHttpClient<IItsmClient, JiraItsmClient>();
builder.Services.AddHttpClient("SiemWebhook");
builder.Services.Configure<CmdbOptions>(builder.Configuration.GetSection("Cmdb"));
var cmdbProvider = builder.Configuration.GetValue<string>("Cmdb:Provider") ?? "Insight";
if (cmdbProvider.Equals("Stub", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ICmdbClient, StubCmdbClient>();
}
else
{
    builder.Services.AddHttpClient<ICmdbClient, JiraInsightClient>();
}

var storageProvider = builder.Configuration.GetValue<string>("Storage:Provider") ?? "InMemory";
if (storageProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("PamGateway");
    builder.Services.AddDbContext<PamGatewayDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddScoped<IAccessRequestStore, EfAccessRequestStore>();
    builder.Services.AddScoped<ISessionStore, EfSessionStore>();
    builder.Services.AddScoped<IRecordingStore, EfRecordingStore>();
    builder.Services.AddScoped<ITargetStore, EfTargetStore>();
    builder.Services.AddScoped<IAuditStore, EfAuditStore>();
    builder.Services.AddScoped<IRoleStore, EfRoleStore>();
    builder.Services.AddScoped<IPolicyStore, EfPolicyStore>();
    builder.Services.AddScoped<IApprovalStore, EfApprovalStore>();
}
else if (storageProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("PamGateway");
    builder.Services.AddDbContext<PamGatewayDbContext>(options => options.UseSqlServer(connectionString));
    builder.Services.AddScoped<IAccessRequestStore, EfAccessRequestStore>();
    builder.Services.AddScoped<ISessionStore, EfSessionStore>();
    builder.Services.AddScoped<IRecordingStore, EfRecordingStore>();
    builder.Services.AddScoped<ITargetStore, EfTargetStore>();
    builder.Services.AddScoped<IAuditStore, EfAuditStore>();
    builder.Services.AddScoped<IRoleStore, EfRoleStore>();
    builder.Services.AddScoped<IPolicyStore, EfPolicyStore>();
    builder.Services.AddScoped<IApprovalStore, EfApprovalStore>();
}
else
{
    builder.Services.AddSingleton<IAccessRequestStore, InMemoryAccessRequestStore>();
    builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
    builder.Services.AddSingleton<IRecordingStore, InMemoryRecordingStore>();
    builder.Services.AddSingleton<ITargetStore, InMemoryTargetStore>();
    builder.Services.AddSingleton<IAuditStore, InMemoryAuditStore>();
    builder.Services.AddSingleton<IRoleStore, InMemoryRoleStore>();
    builder.Services.AddSingleton<IPolicyStore, InMemoryPolicyStore>();
    builder.Services.AddSingleton<IApprovalStore, InMemoryApprovalStore>();
}

builder.Services.AddSingleton<IAgentStore, InMemoryAgentStore>();
builder.Services.AddSingleton<IAgentTicketStore, InMemoryAgentTicketStore>();
builder.Services.AddSingleton<IRecordingStorage>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RecordingStorageOptions>>().Value;
    if (options.Provider.Equals("S3", StringComparison.OrdinalIgnoreCase)
        || options.Provider.Equals("Minio", StringComparison.OrdinalIgnoreCase))
    {
        return new S3RecordingStorage(options);
    }

    return new LocalRecordingStorage(options);
});
builder.Services.AddSingleton<DemoDataSeeder>();
builder.Services.AddHostedService<PamGateway.Api.Services.CmdbSyncService>();
builder.Services.AddHostedService<PamGateway.Api.Services.SiemExportService>();

var app = builder.Build();
await app.Services.GetRequiredService<PamGateway.Api.Services.SystemDataSeeder>().SeedAsync();
await app.Services.GetRequiredService<DemoDataSeeder>().SeedAsync(app.Lifetime.ApplicationStopping);

app.UseMiddleware<PamGateway.Api.Middleware.GlobalExceptionMiddleware>();
app.UseMiddleware<PamGateway.Api.Middleware.AuditImmutabilityMiddleware>();
app.UseRateLimiter();

if (authEnabled)
{
    app.UseAuthentication();
}
app.UseWebSockets();
app.UseAuthorization();
app.Map("/ws/sessions/{sessionId}", async (
    HttpContext context,
    ISessionStore sessions,
    ITargetStore targets,
    IAgentStore agents) =>
{
    var sessionId = context.Request.RouteValues["sessionId"]?.ToString();
    if (string.IsNullOrWhiteSpace(sessionId))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Missing sessionId");
        return;
    }

    var session = sessions.GetById(sessionId);
    if (session is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("Session not found");
        return;
    }

    if (session.Status != SessionStatus.Active)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsync("Session is not active");
        return;
    }

    var agentId = context.Request.Query["agentId"].ToString();
    if (string.IsNullOrWhiteSpace(agentId))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Missing agentId");
        return;
    }

    var agent = agents.GetById(agentId);
    if (agent is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("Agent not found");
        return;
    }

    if (agent.Status != AgentStatus.Online)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsync("Agent is not online");
        return;
    }

    if (string.IsNullOrWhiteSpace(agent.PublicUrl))
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsync("Agent publicUrl is not configured");
        return;
    }

    var target = targets.GetById(session.TargetId);
    if (target is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("Target not found");
        return;
    }

    if (string.IsNullOrWhiteSpace(target.Host) || target.Port is null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Target host/port is not configured");
        return;
    }

    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Expected WebSocket request");
        return;
    }

    using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();
    using var agentSocket = new ClientWebSocket();
    var agentBase = agent.PublicUrl.TrimEnd('/');
    var agentUrl = agentBase.Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
        .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase);
    var targetHost = Uri.EscapeDataString(target.Host);
    var agentWsUrl = $"{agentUrl}/ws/agent/sessions/{session.Id}?targetHost={targetHost}&targetPort={target.Port}";
    await agentSocket.ConnectAsync(new Uri(agentWsUrl), context.RequestAborted);

    var clientToAgent = Task.Run(async () =>
    {
        var buffer = new byte[8192];
        while (!context.RequestAborted.IsCancellationRequested && clientSocket.State == WebSocketState.Open)
        {
            var result = await clientSocket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary)
            {
                await agentSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    context.RequestAborted);
            }
        }
    }, context.RequestAborted);

    var agentToClient = Task.Run(async () =>
    {
        var buffer = new byte[8192];
        while (!context.RequestAborted.IsCancellationRequested && clientSocket.State == WebSocketState.Open)
        {
            var result = await agentSocket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            await clientSocket.SendAsync(
                new ArraySegment<byte>(buffer, 0, result.Count),
                result.MessageType,
                result.EndOfMessage,
                context.RequestAborted);
        }
    }, context.RequestAborted);

    await Task.WhenAny(clientToAgent, agentToClient);
    if (clientSocket.State == WebSocketState.Open)
    {
        await clientSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", context.RequestAborted);
    }
    if (agentSocket.State == WebSocketState.Open)
    {
        await agentSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", context.RequestAborted);
    }
}).RequireAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
