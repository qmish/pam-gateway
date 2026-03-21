using System.Diagnostics;
using Microsoft.Extensions.Options;
using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Worker;

public sealed class SlaOptions
{
    public int EscalationTimeoutMinutes { get; set; } = 60;
    public bool Enabled { get; set; } = true;
}

public sealed class AccessRequestWorker : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("PamGateway.Worker");
    private readonly ILogger<AccessRequestWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerHealthState _healthState;

    public AccessRequestWorker(ILogger<AccessRequestWorker> logger, IServiceProvider serviceProvider, WorkerHealthState healthState)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _healthState = healthState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var activity = ActivitySource.StartActivity("WorkerCycle");
            await ProcessExpiredRequests(stoppingToken);
            await RevokeSessionsForExpiredRequests();
            await CleanupExpiredTickets();
            await RunConsistencyCheck();
            await EscalatePendingRequests(stoppingToken);
            _healthState.RecordCycle();
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessExpiredRequests(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var requests = scope.ServiceProvider.GetService<IAccessRequestStore>();
        var targets = scope.ServiceProvider.GetService<ITargetStore>();
        var audit = scope.ServiceProvider.GetService<IAuditStore>();
        var itsm = scope.ServiceProvider.GetService<IItsmClient>();

        if (requests is null || targets is null || audit is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var request in requests.GetAll().ToList())
        {
            if (request.Status == AccessRequestStatus.Expired || request.ExpiresAt > now)
            {
                continue;
            }

            var updated = request with { Status = AccessRequestStatus.Expired };
            requests.Update(updated);

            var target = targets.GetById(request.TargetId);
            audit.Add(new AuditEvent(
                DateTimeOffset.UtcNow,
                "access.expired",
                "system",
                "system",
                "system",
                request.TargetId,
                target?.Name ?? "",
                "expire",
                "success",
                request.Id,
                "",
                "0.0.0.0"));

            if (!string.IsNullOrWhiteSpace(request.ItsmKey) && itsm is not null)
            {
                try
                {
                    await itsm.UpdateStatusAsync(request.ItsmKey, "expired", stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update Jira ticket to expired.");
                }
            }
        }

        _logger.LogInformation("AccessRequestWorker processed at: {Time}", now);
    }

    private Task RevokeSessionsForExpiredRequests()
    {
        using var scope = _serviceProvider.CreateScope();
        var requests = scope.ServiceProvider.GetService<IAccessRequestStore>();
        var sessions = scope.ServiceProvider.GetService<ISessionStore>();
        var audit = scope.ServiceProvider.GetService<IAuditStore>();

        if (requests is null || sessions is null || audit is null) return Task.CompletedTask;

        var expiredRequestIds = requests.GetAll()
            .Where(r => r.Status == AccessRequestStatus.Expired)
            .Select(r => r.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var session in sessions.GetAll().ToList())
        {
            if (session.Status == SessionStatus.Active && expiredRequestIds.Contains(session.RequestId))
            {
                var terminated = session with { Status = SessionStatus.Terminated, EndedAt = DateTimeOffset.UtcNow };
                sessions.Update(terminated);

                audit.Add(new AuditEvent(
                    DateTimeOffset.UtcNow, "session.revoked", "system", "system", "system",
                    session.TargetId, "", "revoke", "success", session.RequestId, session.Id, "0.0.0.0"));

                _logger.LogInformation("Revoked session {SessionId} for expired request {RequestId}.",
                    session.Id, session.RequestId);
            }
        }

        return Task.CompletedTask;
    }

    private Task CleanupExpiredTickets()
    {
        using var scope = _serviceProvider.CreateScope();
        var tickets = scope.ServiceProvider.GetService<IAgentTicketStore>();

        if (tickets is null) return Task.CompletedTask;

        var now = DateTimeOffset.UtcNow;
        int cleaned = 0;

        foreach (var ticket in tickets.GetAll().ToList())
        {
            if (ticket.ExpiresAt <= now)
            {
                tickets.Revoke(ticket.Ticket);
                cleaned++;
            }
        }

        if (cleaned > 0)
            _logger.LogInformation("Cleaned up {Count} expired agent tickets.", cleaned);

        return Task.CompletedTask;
    }

    private Task RunConsistencyCheck()
    {
        using var scope = _serviceProvider.CreateScope();
        var requests = scope.ServiceProvider.GetService<IAccessRequestStore>();
        var sessions = scope.ServiceProvider.GetService<ISessionStore>();
        var audit = scope.ServiceProvider.GetService<IAuditStore>();

        if (requests is null || sessions is null || audit is null) return Task.CompletedTask;

        foreach (var session in sessions.GetAll().ToList())
        {
            if (session.Status != SessionStatus.Active) continue;

            var request = requests.GetById(session.RequestId);
            if (request is null) continue;

            if (request.Status == AccessRequestStatus.Denied || request.Status == AccessRequestStatus.Expired)
            {
                var terminated = session with { Status = SessionStatus.Terminated, EndedAt = DateTimeOffset.UtcNow };
                sessions.Update(terminated);

                audit.Add(new AuditEvent(
                    DateTimeOffset.UtcNow, "session.consistency_fix", "system", "system", "system",
                    session.TargetId, "", "terminate", "success", session.RequestId, session.Id, "0.0.0.0"));

                _logger.LogWarning("Consistency fix: terminated session {SessionId} — request {RequestId} is {Status}.",
                    session.Id, session.RequestId, request.Status);
            }
        }

        return Task.CompletedTask;
    }

    private async Task EscalatePendingRequests(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var slaOpts = scope.ServiceProvider.GetService<IOptions<SlaOptions>>()?.Value;
        if (slaOpts is null || !slaOpts.Enabled) return;

        var requests = scope.ServiceProvider.GetService<IAccessRequestStore>();
        var audit = scope.ServiceProvider.GetService<IAuditStore>();
        var itsm = scope.ServiceProvider.GetService<IItsmClient>();

        if (requests is null || audit is null) return;

        var now = DateTimeOffset.UtcNow;
        var threshold = now.AddMinutes(-slaOpts.EscalationTimeoutMinutes);

        foreach (var request in requests.GetAll().ToList())
        {
            if (request.Status != AccessRequestStatus.Pending) continue;
            if (request.CreatedAt > threshold) continue;

            audit.Add(new AuditEvent(
                now, "access.sla_escalation", "system", "system", "system",
                request.TargetId, "", "escalate", "warning",
                request.Id, "", $"pending_since={request.CreatedAt:o}"));

            _logger.LogWarning("SLA escalation: request {RequestId} pending since {CreatedAt} (>{Timeout} min).",
                request.Id, request.CreatedAt, slaOpts.EscalationTimeoutMinutes);

            if (!string.IsNullOrWhiteSpace(request.ItsmKey) && itsm is not null)
            {
                for (int retry = 0; retry < 3; retry++)
                {
                    try
                    {
                        await itsm.UpdateStatusAsync(request.ItsmKey, "escalated", stoppingToken);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ITSM escalation retry {Retry}/3 for {Key}", retry + 1, request.ItsmKey);
                        if (retry < 2) await Task.Delay(TimeSpan.FromSeconds(2 * (retry + 1)), stoppingToken);
                    }
                }
            }
        }
    }
}
