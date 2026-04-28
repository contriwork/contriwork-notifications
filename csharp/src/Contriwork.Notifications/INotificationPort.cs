namespace Contriwork.Notifications;

/// <summary>
/// Transport-only notification port. CONTRACT.md §Port.
/// The concrete implementation is <see cref="NotificationClient"/>; consumers
/// may type against this interface to substitute the implementation in tests.
/// </summary>
public interface INotificationPort
{
    /// <summary>
    /// Multicast the payload to every configured adapter in parallel.
    /// See CONTRACT.md §Methods → <c>send</c> for the full semantics.
    /// </summary>
    Task<SendResult> SendAsync(
        Severity severity,
        Payload payload,
        CancellationToken cancellationToken = default);
}
