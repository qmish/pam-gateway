using Microsoft.EntityFrameworkCore;
using PamGateway.Core;
using PamGateway.Data;

namespace PamGateway.Api.Services;

public sealed class AuditRotationOptions
{
    public bool Enabled { get; set; }
    public int RetentionDays { get; set; } = 365;
    public int CheckIntervalHours { get; set; } = 24;
    public int BatchSize { get; set; } = 1000;
}

public sealed class AuditRotationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AuditRotationOptions _options;
    private readonly ILogger<AuditRotationService> _logger;

    public AuditRotationService(
        IServiceProvider serviceProvider,
        Microsoft.Extensions.Options.IOptions<AuditRotationOptions> options,
        ILogger<AuditRotationService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Audit rotation is disabled");
            return;
        }

        _logger.LogInformation("Audit rotation started: retention={Retention} days, interval={Interval}h",
            _options.RetentionDays, _options.CheckIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RotateAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit rotation failed");
            }

            await Task.Delay(TimeSpan.FromHours(_options.CheckIntervalHours), stoppingToken);
        }
    }

    public async Task<int> RotateAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetService<PamGatewayDbContext>();

        if (db is null)
        {
            _logger.LogDebug("Audit rotation skipped — no EF context (InMemory mode)");
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.RetentionDays);
        int totalDeleted = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await db.AuditEvents
                .Where(e => e.Timestamp < cutoff)
                .OrderBy(e => e.Id)
                .Take(_options.BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            db.AuditEvents.RemoveRange(batch);
            await db.SaveChangesAsync(cancellationToken);
            totalDeleted += batch.Count;

            _logger.LogDebug("Audit rotation: deleted batch of {Count} events", batch.Count);
        }

        if (totalDeleted > 0)
        {
            _logger.LogInformation("Audit rotation completed: deleted {Total} events older than {Cutoff:d}",
                totalDeleted, cutoff);
        }

        return totalDeleted;
    }
}
