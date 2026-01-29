using Microsoft.Extensions.Options;
using PamGateway.Core;

namespace PamGateway.Api;

public sealed class DemoDataSeeder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DemoDataOptions _options;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        IServiceProvider serviceProvider,
        IOptions<DemoDataOptions> options,
        ILogger<DemoDataSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Demo data seeding is disabled.");
            return Task.CompletedTask;
        }

        using var scope = _serviceProvider.CreateScope();
        var targets = scope.ServiceProvider.GetRequiredService<ITargetStore>();
        var requests = scope.ServiceProvider.GetRequiredService<IAccessRequestStore>();
        var approvals = scope.ServiceProvider.GetRequiredService<IApprovalStore>();
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionStore>();
        var recordings = scope.ServiceProvider.GetRequiredService<IRecordingStore>();
        var audits = scope.ServiceProvider.GetRequiredService<IAuditStore>();
        var roles = scope.ServiceProvider.GetRequiredService<IRoleStore>();
        var policies = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        var agents = scope.ServiceProvider.GetRequiredService<IAgentStore>();

        var now = DateTimeOffset.UtcNow;

        SeedTargets(targets);
        SeedRoles(roles);
        SeedPolicies(policies);
        var seededRequests = SeedAccessRequests(requests, now);
        SeedApprovals(approvals, now, seededRequests);
        var seededSessions = SeedSessions(sessions, now, seededRequests);
        SeedRecordings(recordings, now, seededSessions);
        SeedAudits(audits, now, seededRequests, seededSessions);
        SeedAgents(agents, now);

        _logger.LogInformation("Demo data seeding completed.");
        return Task.CompletedTask;
    }

    private static void SeedTargets(ITargetStore targets)
    {
        var demoTargets = new[]
        {
            new TargetSystem(
                "TGT-WIN-01",
                "Windows Bastion",
                "win-bastion.local",
                3389,
                new Dictionary<string, string> { ["os"] = "windows", ["access"] = "rdp" },
                "Remote Desktop",
                "prod",
                "critical",
                "Active"),
            new TargetSystem(
                "TGT-LNX-01",
                "Linux Jump Host",
                "linux-jump.local",
                22,
                new Dictionary<string, string> { ["os"] = "linux", ["access"] = "ssh" },
                "SSH",
                "prod",
                "critical",
                "Active"),
            new TargetSystem(
                "TGT-DB-01",
                "Postgres Reporting",
                "pg-reporting.local",
                5432,
                new Dictionary<string, string> { ["role"] = "db", ["access"] = "postgres" },
                "Database",
                "prod",
                "non-critical",
                "Active"),
            new TargetSystem(
                "TGT-NET-01",
                "Firewall Core",
                "fw-core.local",
                443,
                new Dictionary<string, string> { ["role"] = "network", ["access"] = "https" },
                "Network",
                "prod",
                "critical",
                "Active")
        };

        targets.AddOrUpdateRange(demoTargets);
    }

    private void SeedRoles(IRoleStore roles)
    {
        if (_options.SeedIfEmpty && roles.GetAll().Count > 0)
        {
            return;
        }

        roles.Add(new Role("ROLE-OPS", "Ops_Engineer", "Operations engineer with emergency access"));
        roles.Add(new Role("ROLE-SEC", "Security_Auditor", "Security auditor with read-only visibility"));
        roles.Add(new Role("ROLE-WIN", "System_Admin_Windows", "Windows administrators"));
        roles.Add(new Role("ROLE-LNX", "System_Admin_Linux", "Linux administrators"));
    }

    private void SeedPolicies(IPolicyStore policies)
    {
        if (_options.SeedIfEmpty && policies.GetAll().Count > 0)
        {
            return;
        }

        policies.Add(new Policy(
            "POL-RDP-REMOTE",
            "RDP Access",
            "Remote Desktop",
            "rdp",
            "Allow",
            new Dictionary<string, string> { ["os"] = "windows" }));

        policies.Add(new Policy(
            "POL-SSH-SERVER",
            "SSH Access",
            "SSH",
            "ssh",
            "Allow",
            new Dictionary<string, string> { ["os"] = "linux" }));
    }

    private List<AccessRequest> SeedAccessRequests(IAccessRequestStore requests, DateTimeOffset now)
    {
        if (_options.SeedIfEmpty && requests.GetAll().Count > 0)
        {
            return requests.GetAll().ToList();
        }

        var data = new List<AccessRequest>
        {
            new AccessRequest(
                "REQ-1001",
                "TGT-WIN-01",
                "alice",
                60,
                "Emergency patch deployment",
                AccessRequestStatus.Pending,
                now.AddMinutes(-15),
                now.AddMinutes(45),
                "ITSM-1001"),
            new AccessRequest(
                "REQ-1002",
                "TGT-LNX-01",
                "bob",
                120,
                "Routine maintenance",
                AccessRequestStatus.Approved,
                now.AddHours(-2),
                now.AddHours(2),
                "ITSM-1002"),
            new AccessRequest(
                "REQ-1003",
                "TGT-DB-01",
                "carol",
                30,
                "Report export",
                AccessRequestStatus.Denied,
                now.AddHours(-5),
                now.AddHours(-4).AddMinutes(30),
                "ITSM-1003"),
            new AccessRequest(
                "REQ-1004",
                "TGT-NET-01",
                "dan",
                45,
                "Firewall rule review",
                AccessRequestStatus.Expired,
                now.AddHours(-6),
                now.AddHours(-5).AddMinutes(15),
                "ITSM-1004")
        };

        foreach (var request in data)
        {
            requests.Add(request);
        }

        return data;
    }

    private void SeedApprovals(IApprovalStore approvals, DateTimeOffset now, IReadOnlyList<AccessRequest> requests)
    {
        if ((_options.SeedIfEmpty && approvals.GetAll().Count > 0) || requests.Count == 0)
        {
            return;
        }

        var approved = requests.FirstOrDefault(item => item.Status == AccessRequestStatus.Approved);
        var denied = requests.FirstOrDefault(item => item.Status == AccessRequestStatus.Denied);

        if (approved is not null)
        {
            approvals.Add(new Approval("APR-2001", approved.Id, "manager1", now.AddHours(-1), "approved"));
        }

        if (denied is not null)
        {
            approvals.Add(new Approval("APR-2002", denied.Id, "manager2", now.AddHours(-4), "denied"));
        }
    }

    private List<Session> SeedSessions(ISessionStore sessions, DateTimeOffset now, IReadOnlyList<AccessRequest> requests)
    {
        if ((_options.SeedIfEmpty && sessions.GetAll().Count > 0) || requests.Count == 0)
        {
            return sessions.GetAll().ToList();
        }

        var approved = requests.FirstOrDefault(item => item.Status == AccessRequestStatus.Approved);
        if (approved is null)
        {
            return new List<Session>();
        }

        var data = new List<Session>
        {
            new Session(
                "SES-3001",
                approved.TargetId,
                approved.Id,
                "ssh",
                SessionStatus.Active,
                now.AddMinutes(-10),
                null),
            new Session(
                "SES-3002",
                approved.TargetId,
                approved.Id,
                "ssh",
                SessionStatus.Terminated,
                now.AddHours(-3),
                now.AddHours(-2).AddMinutes(-30))
        };

        foreach (var session in data)
        {
            sessions.Add(session);
        }

        return data;
    }

    private void SeedRecordings(IRecordingStore recordings, DateTimeOffset now, IReadOnlyList<Session> sessions)
    {
        if ((_options.SeedIfEmpty && recordings.GetAll().Count > 0) || sessions.Count == 0)
        {
            return;
        }

        var active = sessions.FirstOrDefault(item => item.Status == SessionStatus.Active);
        var ended = sessions.FirstOrDefault(item => item.Status == SessionStatus.Terminated);

        if (active is not null)
        {
            recordings.Add(new SessionRecording(
                "REC-4001",
                active.Id,
                "proxy",
                null,
                RecordingStatus.Recording,
                now.AddMinutes(-9),
                null,
                null,
                null));
        }

        if (ended is not null)
        {
            recordings.Add(new SessionRecording(
                "REC-4002",
                ended.Id,
                "proxy-sync",
                "s3://pam-recordings/recordings/REC-4002.bin",
                RecordingStatus.Completed,
                now.AddHours(-3),
                now.AddHours(-2).AddMinutes(-30),
                10485760,
                "DEMOHASH"));
        }
    }

    private void SeedAudits(IAuditStore audits, DateTimeOffset now, IReadOnlyList<AccessRequest> requests, IReadOnlyList<Session> sessions)
    {
        if (_options.SeedIfEmpty && audits.GetAll().Count > 0)
        {
            return;
        }

        var request = requests.FirstOrDefault();
        var session = sessions.FirstOrDefault();

        audits.Add(new AuditEvent(
            now.AddMinutes(-12),
            "request.created",
            "alice",
            "alice",
            "Ops_Engineer",
            request?.TargetId ?? "TGT-WIN-01",
            "Windows Bastion",
            "create",
            "success",
            request?.Id ?? "REQ-1001",
            session?.Id ?? "",
            "10.10.10.10"));

        audits.Add(new AuditEvent(
            now.AddMinutes(-5),
            "session.started",
            "bob",
            "bob",
            "System_Admin_Linux",
            session?.TargetId ?? "TGT-LNX-01",
            "Linux Jump Host",
            "start",
            "success",
            request?.Id ?? "REQ-1002",
            session?.Id ?? "SES-3001",
            "10.10.20.20"));
    }

    private void SeedAgents(IAgentStore agents, DateTimeOffset now)
    {
        if (_options.SeedIfEmpty && agents.GetAll().Count > 0)
        {
            return;
        }

        agents.Register(new AgentInfo(
            "AGENT-01",
            "agent-win-01",
            "windows",
            AgentStatus.Online,
            now.AddMinutes(-1),
            "http://agent-win-01:8081",
            new Dictionary<string, string> { ["region"] = "dc1" },
            new[] { "rdp", "ssh" },
            Guid.NewGuid().ToString("N")));

        agents.Register(new AgentInfo(
            "AGENT-02",
            "agent-linux-01",
            "linux",
            AgentStatus.Offline,
            now.AddMinutes(-20),
            "http://agent-linux-01:8081",
            new Dictionary<string, string> { ["region"] = "dc2" },
            new[] { "ssh" },
            Guid.NewGuid().ToString("N")));
    }
}
