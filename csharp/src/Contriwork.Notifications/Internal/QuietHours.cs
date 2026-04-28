using System.Globalization;

namespace Contriwork.Notifications.Internal;

internal static class QuietHours
{
    /// <summary>Returns true when <paramref name="now"/> (or wall-clock now) falls inside the window.</summary>
    public static bool IsQuiet(QuietHoursConfig config, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        var tz = TimeZoneInfo.FindSystemTimeZoneById(config.Timezone);
        var moment = now ?? DateTimeOffset.UtcNow;
        var local = TimeZoneInfo.ConvertTime(moment, tz).TimeOfDay;
        var start = ParseHhMm(config.Start);
        var end = ParseHhMm(config.End);
        return start <= end
            ? local >= start && local < end
            : local >= start || local < end;
    }

    private static TimeSpan ParseHhMm(string value)
    {
        return TimeSpan.ParseExact(value, "h\\:mm", CultureInfo.InvariantCulture);
    }
}
