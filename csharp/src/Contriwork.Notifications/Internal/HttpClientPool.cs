namespace Contriwork.Notifications.Internal;

/// <summary>
/// Process-wide shared <see cref="HttpClient"/> used by the bundled HTTP
/// adapters when the consumer does not pass its own instance. SocketsHttpHandler
/// gives modern connection pooling out of the box; sharing the client avoids
/// the socket-exhaustion pitfall of per-call <c>new HttpClient()</c>.
/// </summary>
internal static class HttpClientPool
{
    private static readonly Lazy<HttpClient> Lazy = new(() =>
        new HttpClient(new SocketsHttpHandler())
        {
            Timeout = TimeSpan.FromSeconds(30),
        });

    public static HttpClient Shared => Lazy.Value;
}
