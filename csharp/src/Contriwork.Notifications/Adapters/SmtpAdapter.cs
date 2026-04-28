using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Contriwork.Notifications.Adapters;

/// <summary>
/// SMTP delivery via MailKit. Default transport is STARTTLS on port 587;
/// pass <c>useTls: true</c> + <c>startTls: false</c> for implicit TLS (port 465).
/// </summary>
/// <remarks>
/// Email has no programmatic silent flag; <c>silent</c> is a documented no-op
/// and the wire payload is identical regardless. The orchestrator still
/// reports DELIVERED_SILENT for accounting.
/// </remarks>
public sealed class SmtpAdapter : IAdapter
{
    private const string GenericFailure = "Notification delivery failed";
    private const string GenericAuth = "Authentication rejected";

    private readonly string _host;
    private readonly int _port;
    private readonly string _fromAddr;
    private readonly List<string> _toAddrs;
    private readonly string? _username;
    private readonly string? _password;
    private readonly SecureSocketOptions _secureOptions;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Optional factory for the MailKit transport. Production code leaves it
    /// null and the adapter uses <see cref="SmtpClient"/>; unit tests inject
    /// a mock <see cref="IMailTransport"/> implementation.
    /// </summary>
    public Func<IMailTransport>? TransportFactory { get; init; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>Create an SMTP adapter.</summary>
    public SmtpAdapter(
        string host,
        int port,
        string fromAddr,
        IEnumerable<string> toAddrs,
        string? username = null,
        string? password = null,
        bool useTls = false,
        bool startTls = true,
        string name = "smtp",
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(toAddrs);
        _host = host;
        _port = port;
        _fromAddr = fromAddr;
        _toAddrs = toAddrs.ToList();
        _username = username;
        _password = password;
        _secureOptions = useTls
            ? SecureSocketOptions.SslOnConnect
            : startTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;
        Name = name;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var ok = !string.IsNullOrEmpty(_host)
            && _port > 0
            && !string.IsNullOrEmpty(_fromAddr)
            && _toAddrs.Count > 0;
        return Task.FromResult(ok);
    }

    /// <inheritdoc/>
    public async Task<AdapterDeliverResult> DeliverAsync(
        Severity severity,
        Payload payload,
        bool silent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _ = severity;
        _ = silent; // see class summary; silent is a no-op

        var message = BuildMessage(payload);
        var transport = TransportFactory?.Invoke() ?? new SmtpClient();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);
        var ct = cts.Token;

        try
        {
            await transport.ConnectAsync(_host, _port, _secureOptions, ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
            {
                await transport.AuthenticateAsync(_username, _password, ct).ConfigureAwait(false);
            }
            await transport.SendAsync(message, ct).ConfigureAwait(false);
            await transport.DisconnectAsync(quit: true, ct).ConfigureAwait(false);
        }
        catch (AuthenticationException)
        {
            return new AdapterDeliverResult(AdapterStatus.PermanentFailure, ErrorCode.AuthFailed, GenericAuth);
        }
        catch (SmtpCommandException)
        {
            return new AdapterDeliverResult(AdapterStatus.PermanentFailure, ErrorCode.AdapterInvalidPayload, GenericFailure);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AdapterDeliverResult(AdapterStatus.RetriableFailure, ErrorCode.Timeout, GenericFailure);
        }
#pragma warning disable CA1031 // Generic SMTP/IO failure -> RETRIABLE per CONTRACT.md.
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            return new AdapterDeliverResult(AdapterStatus.RetriableFailure, ErrorCode.UpstreamError, GenericFailure);
        }
        finally
        {
            (transport as IDisposable)?.Dispose();
        }

        return new AdapterDeliverResult(AdapterStatus.Delivered);
    }

    private MimeMessage BuildMessage(Payload payload)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_fromAddr));
        foreach (var to in _toAddrs)
        {
            message.To.Add(MailboxAddress.Parse(to));
        }
        message.Subject = payload.Title;

        var body = payload.Body;
        if (payload.Url is not null)
        {
            var label = payload.UrlTitle ?? payload.Url;
            body += $"\n\n{label}: {payload.Url}";
        }
        message.Body = new TextPart("plain") { Text = body };
        return message;
    }
}
