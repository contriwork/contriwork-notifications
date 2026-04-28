namespace Contriwork.Notifications;

/// <summary>
/// Notification payload. CONTRACT.md §Payload schema.
/// Validation rules (enforced by <see cref="NotificationClient"/> before any
/// adapter is invoked):
/// title non-empty &amp; &lt;= 200 chars; body non-empty &amp; &lt;= 2000 chars;
/// url optional but MUST start with <c>https://</c> when present;
/// url_title optional &amp; &lt;= 100 chars (ignored if url is null);
/// metadata is opaque to the port and passed through to adapters.
/// </summary>
public sealed record Payload(
    string Title,
    string Body,
    string? Url = null,
    string? UrlTitle = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

internal static class PayloadValidator
{
    private const int TitleMax = 200;
    private const int BodyMax = 2000;
    private const int UrlTitleMax = 100;
    private const string HttpsPrefix = "https://";

    /// <summary>
    /// Returns null when the payload is valid, otherwise a short reason string
    /// (informational; the public surface always reports the failure as
    /// <see cref="ErrorCode.InvalidPayload"/> and never echoes the reason).
    /// </summary>
    public static string? Validate(Payload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var title = payload.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            return "title is empty";
        }

        if (title.Length > TitleMax)
        {
            return $"title exceeds {TitleMax} char cap";
        }

        var body = payload.Body?.Trim();
        if (string.IsNullOrEmpty(body))
        {
            return "body is empty";
        }

        if (body.Length > BodyMax)
        {
            return $"body exceeds {BodyMax} char cap";
        }

        if (payload.Url is not null && !payload.Url.StartsWith(HttpsPrefix, StringComparison.Ordinal))
        {
            return "url must use https scheme";
        }

        if (payload.UrlTitle is not null && payload.UrlTitle.Length > UrlTitleMax)
        {
            return $"url_title exceeds {UrlTitleMax} char cap";
        }

        return null;
    }
}
