namespace PamGateway.Core;

public sealed record DeadLetterItem(
    string Id,
    string Operation,
    string TicketKey,
    string Payload,
    string ErrorMessage,
    DateTimeOffset CreatedAt,
    int RetryCount,
    DateTimeOffset? LastRetryAt,
    bool Resolved);

public interface IDeadLetterStore
{
    void Add(DeadLetterItem item);
    IReadOnlyList<DeadLetterItem> GetPending(int limit = 50);
    void MarkResolved(string id);
    void IncrementRetry(string id);
    IReadOnlyList<DeadLetterItem> GetAll();
}

public sealed class InMemoryDeadLetterStore : IDeadLetterStore
{
    private readonly List<DeadLetterItem> _items = new();
    private readonly object _lock = new();

    public void Add(DeadLetterItem item)
    {
        lock (_lock) _items.Add(item);
    }

    public IReadOnlyList<DeadLetterItem> GetPending(int limit = 50)
    {
        lock (_lock)
            return _items.Where(x => !x.Resolved).OrderBy(x => x.CreatedAt).Take(limit).ToList();
    }

    public void MarkResolved(string id)
    {
        lock (_lock)
        {
            var idx = _items.FindIndex(x => x.Id == id);
            if (idx >= 0) _items[idx] = _items[idx] with { Resolved = true };
        }
    }

    public void IncrementRetry(string id)
    {
        lock (_lock)
        {
            var idx = _items.FindIndex(x => x.Id == id);
            if (idx >= 0)
                _items[idx] = _items[idx] with { RetryCount = _items[idx].RetryCount + 1, LastRetryAt = DateTimeOffset.UtcNow };
        }
    }

    public IReadOnlyList<DeadLetterItem> GetAll()
    {
        lock (_lock) return _items.ToList();
    }
}
