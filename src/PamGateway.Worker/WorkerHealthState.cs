namespace PamGateway.Worker;

public sealed class WorkerHealthState
{
    private const string HealthFilePath = "/tmp/worker-healthy";
    private DateTimeOffset _lastCycleAt;

    public DateTimeOffset LastCycleAt => _lastCycleAt;

    public void RecordCycle()
    {
        _lastCycleAt = DateTimeOffset.UtcNow;
        try
        {
            File.WriteAllText(HealthFilePath, _lastCycleAt.ToString("o"));
        }
        catch
        {
            // non-critical: health file write may fail outside K8s
        }
    }

    public bool IsHealthy(TimeSpan maxAge)
    {
        if (_lastCycleAt == default) return false;
        return DateTimeOffset.UtcNow - _lastCycleAt < maxAge;
    }
}
