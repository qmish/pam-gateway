using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
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

var app = builder.Build();

if (authEnabled)
{
    app.UseAuthentication();
}
app.UseAuthorization();
app.MapControllers();

app.Run();
