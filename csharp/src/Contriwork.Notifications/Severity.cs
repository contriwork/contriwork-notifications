namespace Contriwork.Notifications;

/// <summary>
/// Five severity levels. CONTRACT.md §Severity. Order and names are frozen for v1.
/// </summary>
public enum Severity
{
    /// <summary>Diagnostic; silent by design.</summary>
    Debug,

    /// <summary>Informational.</summary>
    Info,

    /// <summary>Recoverable issue.</summary>
    Warn,

    /// <summary>Failure that may need attention.</summary>
    Error,

    /// <summary>Urgent; bypasses quiet hours by default.</summary>
    Critical,
}

/// <summary>
/// Convenience helpers for <see cref="Severity"/> -- the stable public icons
/// and the wire-format string (SCREAMING_SNAKE_CASE) used by the cross-language
/// contract tests.
/// </summary>
public static class SeverityExtensions
{
    /// <summary>Stable icon for the severity (CONTRACT.md severity table).</summary>
    public static string Icon(this Severity severity) => severity switch
    {
        Severity.Debug => "🔍",
        Severity.Info => "ℹ️",
        Severity.Warn => "⚠️",
        Severity.Error => "❌",
        Severity.Critical => "⛔",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };

    /// <summary>Wire-format string ("DEBUG", "INFO", ...) used in the contract fixtures.</summary>
    public static string ToWireString(this Severity severity) => severity switch
    {
        Severity.Debug => "DEBUG",
        Severity.Info => "INFO",
        Severity.Warn => "WARN",
        Severity.Error => "ERROR",
        Severity.Critical => "CRITICAL",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };

    /// <summary>Parse the wire-format string back into a <see cref="Severity"/>.</summary>
    public static Severity FromWireString(string value) => value switch
    {
        "DEBUG" => Severity.Debug,
        "INFO" => Severity.Info,
        "WARN" => Severity.Warn,
        "ERROR" => Severity.Error,
        "CRITICAL" => Severity.Critical,
        _ => throw new ArgumentException($"Unknown severity wire string: '{value}'", nameof(value)),
    };
}
