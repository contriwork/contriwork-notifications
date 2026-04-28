namespace Contriwork.Notifications;

/// <summary>Exponential backoff + jitter retry policy.</summary>
public sealed record RetryConfig(
    int MaxAttempts = 3,
    int BaseDelayMs = 500,
    int MaxDelayMs = 10_000,
    double JitterRatio = 0.2);

/// <summary>
/// Quiet-hours window. Severities listed in <see cref="BypassSeverities"/>
/// ignore the window and are delivered normally. Non-bypassed severities
/// inside the window are delivered in silent mode (DELIVERED_SILENT) -- they
/// are never dropped.
/// </summary>
public sealed record QuietHoursConfig(
    string Start,
    string End,
    string Timezone,
    IReadOnlyList<Severity>? BypassSeverities = null)
{
    /// <summary>Default bypass list: <see cref="Severity.Critical"/> only.</summary>
    public static readonly IReadOnlyList<Severity> DefaultBypass = new[] { Severity.Critical };

    /// <summary>The effective bypass list: caller-provided, falling back to <see cref="DefaultBypass"/>.</summary>
    public IReadOnlyList<Severity> EffectiveBypass => BypassSeverities ?? DefaultBypass;
}

/// <summary>Per-adapter sliding-window business rate-limit policy.</summary>
public sealed record RateLimitPolicy(
    int MaxCount,
    int WindowSeconds,
    IReadOnlyList<Severity>? BypassSeverities = null)
{
    /// <summary>The effective bypass list: caller-provided, falling back to <see cref="QuietHoursConfig.DefaultBypass"/>.</summary>
    public IReadOnlyList<Severity> EffectiveBypass => BypassSeverities ?? QuietHoursConfig.DefaultBypass;
}

/// <summary>
/// Top-level config object passed to <see cref="NotificationClient"/>.
/// All fields are optional. <see cref="RateLimits"/> maps an adapter
/// <see cref="IAdapter.Name"/> to a policy; adapters not present in the
/// dictionary are unconstrained.
/// </summary>
public sealed record NotificationConfig(
    RetryConfig? Retry = null,
    QuietHoursConfig? QuietHours = null,
    IReadOnlyDictionary<string, RateLimitPolicy>? RateLimits = null);
