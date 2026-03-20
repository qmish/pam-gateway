using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Api.Services;

public sealed class CmdbSyncOptions
{
    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 30;
}

public sealed class CmdbSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CmdbSyncOptions _options;
    private readonly ILogger<CmdbSyncService> _logger;

    public CmdbSyncService(
        IServiceProvider serviceProvider,
        Microsoft.Extensions.Options.IOptions<CmdbSyncOptions> options,
        ILogger<CmdbSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("CMDB sync is disabled.");
            return;
        }

        _logger.LogInformation("CMDB sync started, interval: {Interval} min.", _options.IntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(_options.IntervalMinutes), stoppingToken);
        }
    }

    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var cmdb = scope.ServiceProvider.GetRequiredService<ICmdbClient>();
            var targets = scope.ServiceProvider.GetRequiredService<ITargetStore>();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditStore>();

            var existing = targets.GetAll().ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
            var cmdbTargets = await cmdb.FetchTargetsAsync(cancellationToken);

            int created = 0, updated = 0, conflicts = 0;

            foreach (var ct in cmdbTargets)
            {
                var target = new TargetSystem(
                    ct.Id, ct.Name, null, null, null,
                    ct.Type, ct.Environment, ct.Criticality, ct.Status);

                if (existing.TryGetValue(ct.Id, out var old))
                {
                    if (old.Name != target.Name || old.Type != target.Type ||
                        old.Environment != target.Environment || old.Criticality != target.Criticality ||
                        old.Status != target.Status)
                    {
                        targets.AddOrUpdate(target with { Host = old.Host, Port = old.Port, Labels = old.Labels });
                        updated++;
                        _logger.LogInformation("CMDB sync: updated target {Id} ({Name}).", ct.Id, ct.Name);
                    }
                }
                else
                {
                    targets.AddOrUpdate(target);
                    created++;
                    _logger.LogInformation("CMDB sync: created target {Id} ({Name}).", ct.Id, ct.Name);
                }

                existing.Remove(ct.Id);
            }

            foreach (var removed in existing)
            {
                conflicts++;
                _logger.LogWarning("CMDB sync conflict: target {Id} ({Name}) exists locally but absent from CMDB.",
                    removed.Key, removed.Value.Name);
            }

            audit.Add(new AuditEvent(
                DateTimeOffset.UtcNow, "cmdb.sync", "system", "system", "system",
                "", "", "sync", "success", "", "",
                $"created={created},updated={updated},conflicts={conflicts}"));

            _logger.LogInformation("CMDB sync completed: created={Created}, updated={Updated}, conflicts={Conflicts}.",
                created, updated, conflicts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CMDB sync failed.");
        }
    }
}
