using Contriwork.Notifications.Internal;

namespace Contriwork.Notifications;

/// <summary>
/// Transport-only orchestrator: multicast delivery to every adapter in parallel.
/// CONTRACT.md §Multicast semantics.
/// </summary>
public sealed class NotificationClient : INotificationPort
{
    private static readonly RetryConfig DefaultRetry = new();

    private readonly List<IAdapter> _adapters;
    private readonly NotificationConfig _config;
    private readonly RetryConfig _retry;
    private readonly RateLimiter _rateLimiter;

    /// <summary>Create a client with an explicit adapter list.</summary>
    public NotificationClient(IEnumerable<IAdapter> adapters, NotificationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        _adapters = adapters.ToList();
        _config = config ?? new NotificationConfig();
        _retry = _config.Retry ?? DefaultRetry;
        _rateLimiter = new RateLimiter(_config.RateLimits);
    }

    /// <inheritdoc/>
    public async Task<SendResult> SendAsync(
        Severity severity,
        Payload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // 1. Validate payload -- short-circuit before any adapter is invoked.
        if (PayloadValidator.Validate(payload) is not null)
        {
            return new SendResult(Ok: false, Results: Array.Empty<AdapterOutcome>(),
                ErrorCode: ErrorCode.InvalidPayload, Attempts: 0);
        }

        // 2. Empty adapter list -- explicit no-op.
        if (_adapters.Count == 0)
        {
            return new SendResult(Ok: true, Results: Array.Empty<AdapterOutcome>(),
                ErrorCode: null, Attempts: 0);
        }

        // 3. Decide silent mode once for this send.
        var silent = ShouldSilence(severity);

        // 4. Multicast: invoke every adapter in parallel.
        var tasks = _adapters
            .Select(a => InvokeAdapterAsync(a, severity, payload, silent, cancellationToken))
            .ToArray();
        var outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);

        var totalAttempts = outcomes.Sum(o => o.Attempts);
        var anyDelivered = outcomes.Any(o =>
            o.Status is OutcomeStatus.Delivered or OutcomeStatus.DeliveredSilent);

        return anyDelivered
            ? new SendResult(Ok: true, Results: outcomes, ErrorCode: null, Attempts: totalAttempts)
            : new SendResult(Ok: false, Results: outcomes, ErrorCode: ErrorCode.AllAdaptersFailed, Attempts: totalAttempts);
    }

    private bool ShouldSilence(Severity severity)
    {
        var qh = _config.QuietHours;
        if (qh is null)
        {
            return false;
        }
        return !qh.EffectiveBypass.Contains(severity) && QuietHours.IsQuiet(qh);
    }

    private async Task<AdapterOutcome> InvokeAdapterAsync(
        IAdapter adapter,
        Severity severity,
        Payload payload,
        bool silent,
        CancellationToken cancellationToken)
    {
        // 4a. Business rate limit (configured policy).
        if (!IsRateLimitExempt(adapter, severity)
            && !_rateLimiter.Allow(adapter.Name, DateTimeOffset.UtcNow))
        {
            return new AdapterOutcome(
                Adapter: adapter.Name,
                Status: OutcomeStatus.BusinessRateLimited,
                Attempts: 0,
                ErrorCode: ErrorCode.BusinessRateLimited,
                Detail: null);
        }

        // 4b. Availability precheck.
        bool available;
        try
        {
            available = await adapter.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Adapter availability check must never propagate.
        catch
#pragma warning restore CA1031
        {
            available = false;
        }

        if (!available)
        {
            return new AdapterOutcome(
                Adapter: adapter.Name,
                Status: OutcomeStatus.PermanentFailure,
                Attempts: 1,
                ErrorCode: ErrorCode.AdapterUnavailable,
                Detail: null);
        }

        // 4c. Deliver with retry on RETRIABLE_FAILURE.
        AdapterDeliverResult? last = null;
        var attempts = 0;
        for (var attemptIndex = 0; attemptIndex < _retry.MaxAttempts; attemptIndex++)
        {
            attempts++;
            try
            {
                last = await adapter.DeliverAsync(severity, payload, silent, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Adapter exceptions are mapped to RETRIABLE per CONTRACT.md.
            catch
#pragma warning restore CA1031
            {
                last = new AdapterDeliverResult(AdapterStatus.RetriableFailure, ErrorCode.UpstreamError);
            }

            if (last.Status == AdapterStatus.Delivered)
            {
                var status = silent ? OutcomeStatus.DeliveredSilent : OutcomeStatus.Delivered;
                return new AdapterOutcome(
                    Adapter: adapter.Name,
                    Status: status,
                    Attempts: attempts,
                    ErrorCode: null,
                    Detail: last.Detail);
            }

            if (last.Status == AdapterStatus.PermanentFailure)
            {
                return new AdapterOutcome(
                    Adapter: adapter.Name,
                    Status: OutcomeStatus.PermanentFailure,
                    Attempts: attempts,
                    ErrorCode: last.ErrorCode,
                    Detail: last.Detail);
            }

            // RETRIABLE_FAILURE: wait if attempts remain.
            if (attempts < _retry.MaxAttempts)
            {
                var delayMs = ComputeDelayMs(attemptIndex);
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // All attempts exhausted with RETRIABLE_FAILURE.
        return new AdapterOutcome(
            Adapter: adapter.Name,
            Status: OutcomeStatus.RetriableFailure,
            Attempts: attempts,
            ErrorCode: last?.ErrorCode,
            Detail: last?.Detail);
    }

    private bool IsRateLimitExempt(IAdapter adapter, Severity severity)
    {
        var policy = _rateLimiter.PolicyFor(adapter.Name);
        return policy is null || policy.EffectiveBypass.Contains(severity);
    }

    private int ComputeDelayMs(int attemptIndex)
    {
        // delay(n) = min(MaxDelayMs, BaseDelayMs * 2^n) * (1 +/- jitter)
        var baseDelay = _retry.BaseDelayMs * Math.Pow(2, attemptIndex);
        var capped = Math.Min(baseDelay, _retry.MaxDelayMs);
        if (_retry.JitterRatio > 0)
        {
            // Pseudo-random jitter; statistical distribution is sufficient,
            // not used for cryptography.
#pragma warning disable CA5394
            var noise = (Random.Shared.NextDouble() * 2 - 1) * _retry.JitterRatio;
#pragma warning restore CA5394
            capped = Math.Max(0, capped + (capped * noise));
        }
        return (int)capped;
    }
}
