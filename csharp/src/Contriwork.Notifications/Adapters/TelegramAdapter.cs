using System.Text;
using System.Text.Json.Nodes;
using Contriwork.Notifications.Internal;

namespace Contriwork.Notifications.Adapters;

/// <summary>
/// Telegram Bot API delivery via HTTPS POST to
/// <c>api.telegram.org/bot&lt;TOKEN&gt;/sendMessage</c>.
/// </summary>
/// <remarks>
/// Plain-text delivery: title + blank line + body, optionally followed by an
/// url label and url. Severity is not encoded in the wire payload.
/// Silent mode forwards the Bot API <c>disable_notification: true</c> flag.
/// </remarks>
public sealed class TelegramAdapter : IAdapter
{
    private const string DefaultApiBase = "https://api.telegram.org";
    private const string GenericFailure = "Notification delivery failed";
    private const string GenericAuth = "Authentication rejected";

    private readonly string _botToken;
    private readonly string _chatId;
    private readonly Uri _apiBase;
    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>Create a Telegram adapter.</summary>
    public TelegramAdapter(
        string botToken,
        string chatId,
        string name = "telegram",
        string apiBase = DefaultApiBase,
        TimeSpan? timeout = null,
        HttpClient? httpClient = null)
    {
        _botToken = botToken;
        _chatId = chatId;
        Name = name;
        _apiBase = new Uri(apiBase);
        _http = httpClient ?? HttpClientPool.Shared;
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(!string.IsNullOrEmpty(_botToken) && !string.IsNullOrEmpty(_chatId));

    /// <inheritdoc/>
    public async Task<AdapterDeliverResult> DeliverAsync(
        Severity severity,
        Payload payload,
        bool silent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _ = severity; // plain-text delivery; severity not encoded in the wire payload

        var url = new Uri(_apiBase, $"/bot{_botToken}/sendMessage");
        var text = $"{payload.Title}\n\n{payload.Body}";
        if (payload.Url is not null)
        {
            var label = payload.UrlTitle ?? payload.Url;
            text += $"\n\n{label}: {payload.Url}";
        }

        var body = new JsonObject
        {
            ["chat_id"] = _chatId,
            ["text"] = text,
            ["disable_notification"] = silent,
        };

        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

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

    private static AdapterDeliverResult MapResponse(int statusCode) => statusCode switch
    {
        >= 200 and < 300 => new AdapterDeliverResult(AdapterStatus.Delivered),
        429 => new AdapterDeliverResult(AdapterStatus.RetriableFailure, ErrorCode.RateLimited, GenericFailure),
        401 or 403 => new AdapterDeliverResult(AdapterStatus.PermanentFailure, ErrorCode.AuthFailed, GenericAuth),
        >= 400 and < 500 => new AdapterDeliverResult(AdapterStatus.PermanentFailure, ErrorCode.AdapterInvalidPayload, GenericFailure),
        _ => new AdapterDeliverResult(AdapterStatus.RetriableFailure, ErrorCode.UpstreamError, GenericFailure),
    };
}
