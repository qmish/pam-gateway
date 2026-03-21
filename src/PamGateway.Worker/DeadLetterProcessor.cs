using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Worker;

public sealed class DeadLetterProcessor : BackgroundService
{
    private readonly ILogger<DeadLetterProcessor> _logger;
    private readonly IServiceProvider _serviceProvider;

    public DeadLetterProcessor(ILogger<DeadLetterProcessor> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingItems(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    public async Task<int> ProcessPendingItems(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dlq = scope.ServiceProvider.GetService<IDeadLetterStore>();
        var itsm = scope.ServiceProvider.GetService<IItsmClient>();

        if (dlq is null || itsm is null) return 0;

        var pending = dlq.GetPending(20);
        var resolved = 0;

        foreach (var item in pending)
        {
            if (item.RetryCount >= 10)
            {
                _logger.LogError("DLQ item {Id} exceeded max retries, marking resolved.", item.Id);
                dlq.MarkResolved(item.Id);
                resolved++;
                continue;
            }

            try
            {
                if (item.Operation == "update_status")
                {
                    await itsm.UpdateStatusAsync(item.TicketKey, item.Payload, cancellationToken);
                }
                else if (item.Operation == "add_comment")
                {
                    await itsm.AddCommentAsync(item.TicketKey, item.Payload, cancellationToken);
                }

                dlq.MarkResolved(item.Id);
                resolved++;
                _logger.LogInformation("DLQ item {Id} resolved after {Retries} retries.", item.Id, item.RetryCount);
            }
            catch (Exception ex)
            {
                dlq.IncrementRetry(item.Id);
                _logger.LogWarning(ex, "DLQ retry failed for {Id} (retry {Count}).", item.Id, item.RetryCount + 1);
            }
        }

        return resolved;
    }
}
