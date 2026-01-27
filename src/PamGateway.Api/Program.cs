using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Sockets;
using System.Net.WebSockets;
using PamGateway.Api;
using PamGateway.Core;
using PamGateway.Data;
using PamGateway.Integrations;

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);
var authEnabled = builder.Configuration.GetValue<bool?>("Auth:Enabled") ?? true;

builder.Services.AddControllers();
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
    builder.Services.AddSingleton<ITargetStore, InMemoryTargetStore>();
    builder.Services.AddSingleton<IAuditStore, InMemoryAuditStore>();
    builder.Services.AddSingleton<IRoleStore, InMemoryRoleStore>();
    builder.Services.AddSingleton<IPolicyStore, InMemoryPolicyStore>();
    builder.Services.AddSingleton<IApprovalStore, InMemoryApprovalStore>();
}

builder.Services.AddSingleton<IAgentStore, InMemoryAgentStore>();
builder.Services.AddSingleton<IAgentTicketStore, InMemoryAgentTicketStore>();

var app = builder.Build();

if (authEnabled)
{
    app.UseAuthentication();
}
app.UseWebSockets();
app.UseAuthorization();
app.Map("/ws/sessions/{sessionId}", async (
    HttpContext context,
    ISessionStore sessions,
    ITargetStore targets) =>
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

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    using var tcpClient = new TcpClient();
    await tcpClient.ConnectAsync(target.Host, target.Port.Value, context.RequestAborted);
    await using var tcpStream = tcpClient.GetStream();

    var wsToTcp = Task.Run(async () =>
    {
        var buffer = new byte[8192];
        while (!context.RequestAborted.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary)
            {
                await tcpStream.WriteAsync(buffer.AsMemory(0, result.Count), context.RequestAborted);
            }
        }
    }, context.RequestAborted);

    var tcpToWs = Task.Run(async () =>
    {
        var buffer = new byte[8192];
        while (!context.RequestAborted.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var read = await tcpStream.ReadAsync(buffer.AsMemory(0, buffer.Length), context.RequestAborted);
            if (read == 0)
            {
                break;
            }

            await socket.SendAsync(
                new ArraySegment<byte>(buffer, 0, read),
                WebSocketMessageType.Binary,
                true,
                context.RequestAborted);
        }
    }, context.RequestAborted);

    await Task.WhenAny(wsToTcp, tcpToWs);
    if (socket.State == WebSocketState.Open)
    {
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", context.RequestAborted);
    }
}).RequireAuthorization();
app.MapControllers();

app.Run();
