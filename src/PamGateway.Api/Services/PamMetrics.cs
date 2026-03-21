using System.Diagnostics.Metrics;

namespace PamGateway.Api.Services;

public sealed class PamMetrics
{
    public static readonly string MeterName = "PamGateway";

    private readonly Counter<long> _sessionsStarted;
    private readonly Counter<long> _sessionsTerminated;
    private readonly Counter<long> _requestsCreated;
    private readonly Counter<long> _requestsApproved;
    private readonly Counter<long> _requestsDenied;
    private readonly Counter<long> _requestsExpired;
    private readonly Counter<long> _integrationErrors;
    private readonly Counter<long> _policyDenials;

    private readonly UpDownCounter<long> _activeSessions;
    private readonly UpDownCounter<long> _onlineAgents;

    public PamMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _sessionsStarted = meter.CreateCounter<long>("pam.sessions.started", "sessions", "Total sessions started");
        _sessionsTerminated = meter.CreateCounter<long>("pam.sessions.terminated", "sessions", "Total sessions terminated");
        _requestsCreated = meter.CreateCounter<long>("pam.requests.created", "requests", "Total access requests created");
        _requestsApproved = meter.CreateCounter<long>("pam.requests.approved", "requests", "Total access requests approved");
        _requestsDenied = meter.CreateCounter<long>("pam.requests.denied", "requests", "Total access requests denied");
        _requestsExpired = meter.CreateCounter<long>("pam.requests.expired", "requests", "Total access requests expired");
        _integrationErrors = meter.CreateCounter<long>("pam.integration.errors", "errors", "Integration errors (Jira, CMDB, SIEM)");
        _policyDenials = meter.CreateCounter<long>("pam.policy.denials", "denials", "Policy evaluation denials");

        _activeSessions = meter.CreateUpDownCounter<long>("pam.sessions.active", "sessions", "Currently active sessions");
        _onlineAgents = meter.CreateUpDownCounter<long>("pam.agents.online", "agents", "Currently online agents");
    }

    public void SessionStarted() => _sessionsStarted.Add(1);
    public void SessionTerminated() => _sessionsTerminated.Add(1);
    public void RequestCreated() => _requestsCreated.Add(1);
    public void RequestApproved() => _requestsApproved.Add(1);
    public void RequestDenied() => _requestsDenied.Add(1);
    public void RequestExpired() => _requestsExpired.Add(1);
    public void IntegrationError(string integration) => _integrationErrors.Add(1, new KeyValuePair<string, object?>("integration", integration));
    public void PolicyDenial() => _policyDenials.Add(1);

    public void ActiveSessionChanged(int delta) => _activeSessions.Add(delta);
    public void OnlineAgentChanged(int delta) => _onlineAgents.Add(delta);
}
