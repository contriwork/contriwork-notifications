namespace Contriwork.Notifications;

/// <summary>
/// Stable error codes from CONTRACT.md §Error taxonomy. Names are frozen for
/// contract revision v1; new codes may be added but existing ones never
/// change without a contract revision bump.
/// </summary>
public enum ErrorCode
{
    // Client-level

    /// <summary>Payload validation failed.</summary>
    InvalidPayload,

    /// <summary>Package's per-adapter rate-limit policy blocked the call.</summary>
    BusinessRateLimited,

    /// <summary>Every adapter returned a permanent failure or business rate limit.</summary>
    AllAdaptersFailed,

    // Adapter-level

    /// <summary>Adapter is not available (creds missing, platform not supported).</summary>
    AdapterUnavailable,

    /// <summary>Upstream returned HTTP 429 or equivalent.</summary>
    RateLimited,

    /// <summary>Upstream returned 401 / 403.</summary>
    AuthFailed,

    /// <summary>Upstream rejected payload (HTTP 400).</summary>
    AdapterInvalidPayload,

    /// <summary>Network or subprocess timeout.</summary>
    Timeout,

    /// <summary>Upstream HTTP 5xx.</summary>
    UpstreamError,
}

/// <summary>Wire-format helpers for <see cref="ErrorCode"/>.</summary>
public static class ErrorCodeExtensions
{
    /// <summary>SCREAMING_SNAKE_CASE wire string used by the contract fixtures.</summary>
    public static string ToWireString(this ErrorCode code) => code switch
    {
        ErrorCode.InvalidPayload => "INVALID_PAYLOAD",
        ErrorCode.BusinessRateLimited => "BUSINESS_RATE_LIMITED",
        ErrorCode.AllAdaptersFailed => "ALL_ADAPTERS_FAILED",
        ErrorCode.AdapterUnavailable => "ADAPTER_UNAVAILABLE",
        ErrorCode.RateLimited => "RATE_LIMITED",
        ErrorCode.AuthFailed => "AUTH_FAILED",
        ErrorCode.AdapterInvalidPayload => "ADAPTER_INVALID_PAYLOAD",
        ErrorCode.Timeout => "TIMEOUT",
        ErrorCode.UpstreamError => "UPSTREAM_ERROR",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    /// <summary>Parse the wire-format string back into an <see cref="ErrorCode"/>.</summary>
    public static ErrorCode FromWireString(string value) => value switch
    {
        "INVALID_PAYLOAD" => ErrorCode.InvalidPayload,
        "BUSINESS_RATE_LIMITED" => ErrorCode.BusinessRateLimited,
        "ALL_ADAPTERS_FAILED" => ErrorCode.AllAdaptersFailed,
        "ADAPTER_UNAVAILABLE" => ErrorCode.AdapterUnavailable,
        "RATE_LIMITED" => ErrorCode.RateLimited,
        "AUTH_FAILED" => ErrorCode.AuthFailed,
        "ADAPTER_INVALID_PAYLOAD" => ErrorCode.AdapterInvalidPayload,
        "TIMEOUT" => ErrorCode.Timeout,
        "UPSTREAM_ERROR" => ErrorCode.UpstreamError,
        _ => throw new ArgumentException($"Unknown error code wire string: '{value}'", nameof(value)),
    };
}
