using System.Globalization;
using Contriwork.Notifications.Internal;

namespace Contriwork.Notifications.Adapters;

/// <summary>
/// Pushover delivery via HTTPS POST to <c>api.pushover.net/1/messages.json</c>.
/// </summary>
/// <remarks>
/// <para>Severity → Pushover priority mapping:</para>
/// <list type="bullet">
///   <item>Debug    → -1 (silent / badge-only)</item>
///   <item>Info     →  0</item>
///   <item>Warn     →  0</item>
///   <item>Error    →  <c>errorPriority</c> (default 0; pass 1 to escalate)</item>
///   <item>Critical →  1 (high priority)</item>
/// </list>
/// <para>Silent mode: priority is forced to -1. Severities listed in the
/// client's bypass list never reach silent mode in the first place.</para>
/// </remarks>
public sealed class PushoverAdapter : IAdapter
{
    private const string DefaultApiUrl = "https://api.pushover.net/1/messages.json";
    private const string GenericFailure = "Notification delivery failed";
    private const string GenericAuth = "Authentication rejected";

    private readonly string _userKey;
    private readonly string _appToken;
    private readonly int _errorPriority;
    private readonly Uri _apiUri;
    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>Create a Pushover adapter.</summary>
    public PushoverAdapter(
        string userKey,
        string appToken,
        string name = "pushover",
        int errorPriority = 0,
        string apiUrl = DefaultApiUrl,
        TimeSpan? timeout = null,
        HttpClient? httpClient = null)
    {
        if (errorPriority is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(errorPriority), errorPriority,
                "errorPriority must be 0 or 1");
        }
        _userKey = userKey;
        _appToken = appToken;
        Name = name;
        _errorPriority = errorPriority;
        _apiUri = new Uri(apiUrl);
        _http = httpClient ?? HttpClientPool.Shared;
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(!string.IsNullOrEmpty(_userKey) && !string.IsNullOrEmpty(_appToken));

    /// <inheritdoc/>
    public async Task<AdapterDeliverResult> DeliverAsync(
        Severity severity,
        Payload payload,
        bool silent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var priority = silent ? -1 : PriorityFor(severity);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["token"] = _appToken,
            ["user"] = _userKey,
            ["title"] = payload.Title,
            ["message"] = payload.Body,
            ["priority"] = priority.ToString(CultureInfo.InvariantCulture),
        };
        if (payload.Url is not null)
        {
            fields["url"] = payload.Url;
        }
        if (payload.UrlTitle is not null)
        {
            fields["url_title"] = payload.UrlTitle;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _apiUri)
        {
            Content = new FormUrlEncodedContent(fields),
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AdapterDeliverResult(AdapterStatus.RetriableFailure, ErrorCode.Timeout, GenericFailure);
        }
        catch (HttpRequestException)
        {
            return new AdapterDeliverResult(AdapterStatus.RetriableFailure, ErrorCode.UpstreamError, GenericFailure);
        }

        try
        {
            return MapResponse((int)response.StatusCode);
        }
        finally
        {
            response.Dispose();
        }
    }

    private int PriorityFor(Severity severity) => severity switch
    {
        Severity.Debug => -1,
        Severity.Critical => 1,
        Severity.Error => _errorPriority,
        _ => 0,
    };

    private static AdapterDeliverResult MapResponse(int statusCode) => statusCode switch
    {
        >= 200 and < 300 => new AdapterDeliverResult(AdapterStatus.Delivered),
        429 => new AdapterDeliverResult(AdapterStatus.RetriableFailure, ErrorCode.RateLimited, GenericFailure),
        401 or 403 => new AdapterDeliverResult(AdapterStatus.PermanentFailure, ErrorCode.AuthFailed, GenericAuth),
        >= 400 and < 500 => new AdapterDeliverResult(AdapterStatus.PermanentFailure, ErrorCode.AdapterInvalidPayload, GenericFailure),
        _ => new AdapterDeliverResult(AdapterStatus.RetriableFailure, ErrorCode.UpstreamError, GenericFailure),
    };
}
