namespace Contriwork.Notifications.Adapters;

/// <summary>
/// In-memory test/reference adapter driven by a fixed outcome sequence.
/// </summary>
/// <remarks>
/// Each call to <see cref="DeliverAsync"/> consumes the next entry in
/// <see cref="Behaviors"/>; once the sequence is exhausted the last entry
/// repeats (matching the contract-tests fixture schema). Every invocation is
/// recorded in <see cref="Calls"/> so unit tests can assert on the observed
/// severity, payload, and silent flag.
/// </remarks>
public sealed class InMemoryAdapter : IAdapter
{
    private readonly List<AdapterDeliverResult> _behaviors;
    private int _index;

    /// <summary>Recorded per-invocation triples; the test inspects this list.</summary>
    public List<(Severity Severity, Payload Payload, bool Silent)> Calls { get; } = new();

    /// <summary>Outcome sequence consumed by <see cref="DeliverAsync"/>.</summary>
    public IReadOnlyList<AdapterDeliverResult> Behaviors => _behaviors;

    /// <summary>Value returned by <see cref="IsAvailableAsync"/>.</summary>
    public bool Available { get; init; } = true;

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>Create an adapter with a (possibly empty) outcome sequence.</summary>
    public InMemoryAdapter(string name = "memory", IEnumerable<AdapterDeliverResult>? behaviors = null)
    {
        Name = name;
        _behaviors = behaviors?.ToList() ?? new List<AdapterDeliverResult>();
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Available);

    /// <inheritdoc/>
    public Task<AdapterDeliverResult> DeliverAsync(
        Severity severity,
        Payload payload,
        bool silent,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((severity, payload, silent));
        if (_behaviors.Count == 0)
        {
            return Task.FromResult(new AdapterDeliverResult(AdapterStatus.Delivered));
        }

        var idx = Math.Min(_index, _behaviors.Count - 1);
        _index++;
        return Task.FromResult(_behaviors[idx]);
    }
}
