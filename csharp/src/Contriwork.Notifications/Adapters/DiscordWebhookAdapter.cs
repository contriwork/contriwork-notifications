using System.Text;
using System.Text.Json.Nodes;
using Contriwork.Notifications.Internal;

namespace Contriwork.Notifications.Adapters;

/// <summary>
/// Discord webhook delivery via HTTPS POST.
/// </summary>
/// <remarks>
/// The body is sent as plain Markdown content (<c>**title**</c> + body,
/// optional <c>[label](url)</c> link). Silent mode sets the message
/// <c>flags</c> field to 4096 (<c>SUPPRESS_NOTIFICATIONS</c>).
/// </remarks>
public sealed class DiscordWebhookAdapter : IAdapter
{
    private const int SuppressNotificationsFlag = 4096;
    private const string GenericFailure = "Notification delivery failed";
    private const string GenericAuth = "Authentication rejected";

    private readonly Uri _webhookUri;
    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>Create a Discord webhook adapter.</summary>
    public DiscordWebhookAdapter(
        string webhookUrl,
        string name = "discord-webhook",
        TimeSpan? timeout = null,
        HttpClient? httpClient = null)
    {
        _webhookUri = new Uri(webhookUrl);
        Name = name;
        _http = httpClient ?? HttpClientPool.Shared;
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <inheritdoc/>
    public async Task<AdapterDeliverResult> DeliverAsync(
        Severity severity,
        Payload payload,
        bool silent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _ = severity;

        var content = $"**{payload.Title}**\n{payload.Body}";
        if (payload.Url is not null)
        {
            var label = payload.UrlTitle ?? payload.Url;
            content += $"\n[{label}]({payload.Url})";
        }

        var body = new JsonObject { ["content"] = content };
        if (silent)
        {
            body["flags"] = SuppressNotificationsFlag;
        }

        using var stringContent = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, _webhookUri) { Content = stringContent };

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
