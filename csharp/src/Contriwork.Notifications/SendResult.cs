namespace Contriwork.Notifications;

/// <summary>Per-adapter outcome status reported by the orchestrator.</summary>
public enum OutcomeStatus
{
    /// <summary>Delivered normally.</summary>
    Delivered,

    /// <summary>Delivered in silent mode (active quiet hours).</summary>
    DeliveredSilent,

    /// <summary>Adapter ran out of retries on retriable failures.</summary>
    RetriableFailure,

    /// <summary>Adapter returned a permanent failure or was unavailable.</summary>
    PermanentFailure,

    /// <summary>Adapter was short-circuited by the package's per-adapter rate-limit policy.</summary>
    BusinessRateLimited,
}

/// <summary>Wire-format helpers for <see cref="OutcomeStatus"/>.</summary>
public static class OutcomeStatusExtensions
{
    /// <summary>SCREAMING_SNAKE_CASE wire string used by the contract fixtures.</summary>
    public static string ToWireString(this OutcomeStatus status) => status switch
    {
        OutcomeStatus.Delivered => "DELIVERED",
        OutcomeStatus.DeliveredSilent => "DELIVERED_SILENT",
        OutcomeStatus.RetriableFailure => "RETRIABLE_FAILURE",
        OutcomeStatus.PermanentFailure => "PERMANENT_FAILURE",
        OutcomeStatus.BusinessRateLimited => "BUSINESS_RATE_LIMITED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>Parse the wire-format string back into an <see cref="OutcomeStatus"/>.</summary>
    public static OutcomeStatus FromWireString(string value) => value switch
    {
        "DELIVERED" => OutcomeStatus.Delivered,
        "DELIVERED_SILENT" => OutcomeStatus.DeliveredSilent,
        "RETRIABLE_FAILURE" => OutcomeStatus.RetriableFailure,
        "PERMANENT_FAILURE" => OutcomeStatus.PermanentFailure,
        "BUSINESS_RATE_LIMITED" => OutcomeStatus.BusinessRateLimited,
        _ => throw new ArgumentException($"Unknown outcome status wire string: '{value}'", nameof(value)),
    };
}

/// <summary>Aggregate outcome reported per adapter, after retries.</summary>
public sealed record AdapterOutcome(
    string Adapter,
    OutcomeStatus Status,
    int Attempts,
    ErrorCode? ErrorCode = null,
    string? Detail = null);

/// <summary>Result of a single <see cref="NotificationClient.SendAsync"/> call.</summary>
public sealed record SendResult(
    bool Ok,
    IReadOnlyList<AdapterOutcome> Results,
    ErrorCode? ErrorCode = null,
    int Attempts = 0);
