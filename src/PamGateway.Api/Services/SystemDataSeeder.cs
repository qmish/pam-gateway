using PamGateway.Core;

namespace PamGateway.Api.Services;

public sealed class SystemDataSeeder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SystemDataSeeder> _logger;

    public SystemDataSeeder(IServiceProvider serviceProvider, ILogger<SystemDataSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task SeedAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<IRoleStore>();
        var policies = scope.ServiceProvider.GetRequiredService<IPolicyStore>();

        SeedRoles(roles);
        SeedPolicies(policies);

        _logger.LogInformation("System data seed completed.");
        return Task.CompletedTask;
    }

    private void SeedRoles(IRoleStore roles)
    {
        if (roles.GetAll().Count > 0) return;

        var systemRoles = new (string Id, string Name, string Description)[]
        {
            ("ROLE-ADMIN",  "PAM_Administrator",    "Full PAM system administrator"),
            ("ROLE-SEC",    "Security_Auditor",     "Security auditor with read-only visibility"),
            ("ROLE-WIN",    "System_Admin_Windows",  "Windows administrators"),
            ("ROLE-LNX",    "System_Admin_Linux",    "Linux administrators"),
            ("ROLE-DBA",    "DB_Admin",             "Database administrators"),
            ("ROLE-NET",    "Network_Admin",         "Network device administrators"),
            ("ROLE-1C",     "OneC_Admin",           "1C platform administrators"),
            ("ROLE-APP",    "App_Support",          "Application support engineers"),
            ("ROLE-OPS",    "Ops_Engineer",         "Operations engineer with emergency access"),
            ("ROLE-DEVOPS", "DevOps",               "DevOps engineers"),
            ("ROLE-SD",     "ServiceDesk",          "Service desk operators"),
        };

        foreach (var (id, name, description) in systemRoles)
            roles.Add(new Role(id, name, description));

        _logger.LogInformation("Seeded {Count} system roles.", systemRoles.Length);
    }

    private void SeedPolicies(IPolicyStore policies)
    {
        if (policies.GetAll().Count > 0) return;

        policies.Add(new Policy("POL-RDP-REMOTE", "RDP Access", "Remote Desktop", "rdp", "Allow",
            new Dictionary<string, string> { ["os"] = "windows" }));
        policies.Add(new Policy("POL-SSH-SERVER", "SSH Access", "SSH", "ssh", "Allow",
            new Dictionary<string, string> { ["os"] = "linux" }));
        policies.Add(new Policy("POL-DB-ACCESS", "Database Access", "Database", "postgres,mysql,mssql", "Allow",
            new Dictionary<string, string> { ["role"] = "db" }));
        policies.Add(new Policy("POL-NET-ACCESS", "Network Device Access", "Network", "ssh,https", "Allow",
            new Dictionary<string, string> { ["role"] = "network" }));

        _logger.LogInformation("Seeded 4 system policies.");
    }
}
