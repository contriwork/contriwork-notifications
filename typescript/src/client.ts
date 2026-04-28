import type { Adapter, AdapterDeliverResult } from "./adapter";
import { AdapterStatus } from "./adapter";
import type { NotificationConfig, RetryConfig } from "./config";
import { DEFAULT_BYPASS_SEVERITIES, DEFAULT_RETRY } from "./config";
import { ErrorCode } from "./errors";
import { isQuiet } from "./internal/quiet-hours";
import { RateLimiter } from "./internal/rate-limit";
import type { Payload } from "./payload";
import { validatePayload } from "./payload";
import type { NotificationPort } from "./port";
import type { AdapterOutcome, SendResult } from "./result";
import { OutcomeStatus } from "./result";
import type { Severity } from "./severity";

/**
 * Transport-only orchestrator: multicast delivery to every adapter in
 * parallel. CONTRACT.md §Multicast semantics.
 *
 * The client never persists messages, deduplicates, routes by named
 * channels, or owns configuration sources. Anything beyond send / retry /
 * quiet-hours silent downgrade / per-adapter business rate limit is the
 * consumer's responsibility.
 */
export class NotificationClient implements NotificationPort {
  private readonly adapters: ReadonlyArray<Adapter>;
  private readonly config: NotificationConfig;
  private readonly retry: RetryConfig;
  private readonly rateLimiter: RateLimiter;

  constructor(adapters: Iterable<Adapter>, config?: NotificationConfig) {
    this.adapters = [...adapters];
    this.config = config ?? {};
    this.retry = this.config.retry ?? DEFAULT_RETRY;
    this.rateLimiter = new RateLimiter(this.config.rateLimits);
  }

  async send(severity: Severity, payload: Payload): Promise<SendResult> {
    if (validatePayload(payload) !== null) {
      return {
        ok: false,
        results: [],
        errorCode: ErrorCode.InvalidPayload,
        attempts: 0,
      };
    }

    if (this.adapters.length === 0) {
      return { ok: true, results: [], attempts: 0 };
    }

    const silent = this.shouldSilence(severity);

    const tasks = this.adapters.map((a) => this.invokeAdapter(a, severity, payload, silent));
    const outcomes = await Promise.all(tasks);

    const totalAttempts = outcomes.reduce((sum, o) => sum + o.attempts, 0);
    const anyDelivered = outcomes.some(
      (o) => o.status === OutcomeStatus.Delivered || o.status === OutcomeStatus.DeliveredSilent,
    );

    if (anyDelivered) {
      return { ok: true, results: outcomes, attempts: totalAttempts };
    }
    return {
      ok: false,
      results: outcomes,
      errorCode: ErrorCode.AllAdaptersFailed,
      attempts: totalAttempts,
    };
  }

  private shouldSilence(severity: Severity): boolean {
    const qh = this.config.quietHours;
    if (!qh) return false;
    const bypass = qh.bypassSeverities ?? DEFAULT_BYPASS_SEVERITIES;
    if (bypass.includes(severity)) return false;
    return isQuiet(qh);
  }

  private async invokeAdapter(
    adapter: Adapter,
    severity: Severity,
    payload: Payload,
    silent: boolean,
  ): Promise<AdapterOutcome> {
    if (
      !this.isRateLimitExempt(adapter, severity) &&
      !this.rateLimiter.allow(adapter.name, Date.now())
    ) {
      return {
        adapter: adapter.name,
        status: OutcomeStatus.BusinessRateLimited,
        attempts: 0,
        errorCode: ErrorCode.BusinessRateLimited,
      };
    }

    let available: boolean;
    try {
      available = await adapter.isAvailable();
    } catch {
      available = false;
    }
    if (!available) {
      return {
        adapter: adapter.name,
        status: OutcomeStatus.PermanentFailure,
        attempts: 1,
        errorCode: ErrorCode.AdapterUnavailable,
      };
    }

    let last: AdapterDeliverResult | undefined;
    let attempts = 0;
    for (let i = 0; i < this.retry.maxAttempts; i++) {
      attempts++;
      try {
        last = await adapter.deliver(severity, payload, silent);
      } catch {
        last = { status: AdapterStatus.RetriableFailure, errorCode: ErrorCode.UpstreamError };
      }

      if (last.status === AdapterStatus.Delivered) {
        const status = silent ? OutcomeStatus.DeliveredSilent : OutcomeStatus.Delivered;
        const outcome: AdapterOutcome = {
          adapter: adapter.name,
          status,
          attempts,
          ...(last.detail !== undefined ? { detail: last.detail } : {}),
        };
        return outcome;
      }
      if (last.status === AdapterStatus.PermanentFailure) {
        return {
          adapter: adapter.name,
          status: OutcomeStatus.PermanentFailure,
          attempts,
          ...(last.errorCode !== undefined ? { errorCode: last.errorCode } : {}),
          ...(last.detail !== undefined ? { detail: last.detail } : {}),
        };
      }

      if (attempts < this.retry.maxAttempts) {
        const delayMs = this.computeDelay(i);
        if (delayMs > 0) {
          await new Promise((resolve) => setTimeout(resolve, delayMs));
        }
      }
    }

    return {
      adapter: adapter.name,
      status: OutcomeStatus.RetriableFailure,
      attempts,
      ...(last?.errorCode !== undefined ? { errorCode: last.errorCode } : {}),
      ...(last?.detail !== undefined ? { detail: last.detail } : {}),
    };
  }

  private isRateLimitExempt(adapter: Adapter, severity: Severity): boolean {
    const policy = this.rateLimiter.policyFor(adapter.name);
    if (!policy) return true;
    const bypass = policy.bypassSeverities ?? DEFAULT_BYPASS_SEVERITIES;
    return bypass.includes(severity);
  }

  private computeDelay(attemptIndex: number): number {
    const base = this.retry.baseDelayMs * Math.pow(2, attemptIndex);
    let capped = Math.min(base, this.retry.maxDelayMs);
    if (this.retry.jitterRatio > 0) {
      const noise = (Math.random() * 2 - 1) * this.retry.jitterRatio;
      capped = Math.max(0, capped + capped * noise);
    }
    return Math.floor(capped);
  }
}
