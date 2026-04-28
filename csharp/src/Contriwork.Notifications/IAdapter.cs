namespace Contriwork.Notifications;

/// <summary>Outcome of a single <see cref="IAdapter.DeliverAsync"/> call.</summary>
public enum AdapterStatus
{
    /// <summary>The adapter delivered the payload to the upstream service.</summary>
    Delivered,

    /// <summary>The call failed, but the failure may succeed if retried.</summary>
    RetriableFailure,

    /// <summary>The call failed and will not succeed if retried.</summary>
    PermanentFailure,
}

/// <summary>Result of one <see cref="IAdapter.DeliverAsync"/> invocation.</summary>
public sealed record AdapterDeliverResult(
    AdapterStatus Status,
    ErrorCode? ErrorCode = null,
    string? Detail = null);

/// <summary>
/// Adapter contract. CONTRACT.md §Adapter protocol.
/// Implementations MUST: expose a stable string <c>Name</c>; return cheaply
/// from <see cref="IsAvailableAsync"/>; translate <c>silent=true</c> into the
/// channel's nearest equivalent silent representation, or document a no-op.
/// </summary>
public interface IAdapter
{
    /// <summary>Stable identifier (e.g. <c>"pushover"</c>, <c>"slack-webhook"</c>).</summary>
    string Name { get; }

    /// <summary>Cheap precheck (creds present, platform supported, etc.).</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Send one notification. When <paramref name="silent"/> is true the adapter
    /// MUST translate it into the channel's nearest silent representation, or
    /// document a no-op explicitly.
    /// </summary>
    Task<AdapterDeliverResult> DeliverAsync(
        Severity severity,
        Payload payload,
        bool silent,
        CancellationToken cancellationToken = default);
}
