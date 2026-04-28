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

/// <summary>Wire-format helpers for <see cref="AdapterStatus"/>.</summary>
public static class AdapterStatusExtensions
{
    /// <summary>SCREAMING_SNAKE_CASE wire string used by the contract fixtures.</summary>
    public static string ToWireString(this AdapterStatus status) => status switch
    {
        AdapterStatus.Delivered => "DELIVERED",
        AdapterStatus.RetriableFailure => "RETRIABLE_FAILURE",
        AdapterStatus.PermanentFailure => "PERMANENT_FAILURE",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>Parse the wire-format string back into an <see cref="AdapterStatus"/>.</summary>
    public static AdapterStatus FromWireString(string value) => value switch
    {
        "DELIVERED" => AdapterStatus.Delivered,
        "RETRIABLE_FAILURE" => AdapterStatus.RetriableFailure,
        "PERMANENT_FAILURE" => AdapterStatus.PermanentFailure,
        _ => throw new ArgumentException($"Unknown adapter status wire string: '{value}'", nameof(value)),
    };
}

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
