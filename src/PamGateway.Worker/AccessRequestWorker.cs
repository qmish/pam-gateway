using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Worker;

public sealed class AccessRequestWorker : BackgroundService
{
    private readonly ILogger<AccessRequestWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public AccessRequestWorker(ILogger<AccessRequestWorker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessExpiredRequests(stoppingToken);
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
        await Task.CompletedTask;
    }
}
