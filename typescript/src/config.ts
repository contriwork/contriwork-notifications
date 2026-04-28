import { Severity } from "./severity";

/** Exponential backoff + jitter retry policy. */
export interface RetryConfig {
  readonly maxAttempts: number;
  readonly baseDelayMs: number;
  readonly maxDelayMs: number;
  readonly jitterRatio: number;
}

/**
 * Quiet-hours window. Severities listed in `bypassSeverities` ignore the
 * window and are delivered normally. Non-bypassed severities inside the
 * window are delivered in silent mode (DELIVERED_SILENT) — they are never
 * dropped.
 */
export interface QuietHoursConfig {
  readonly start: string; // "HH:MM" wall-clock
  readonly end: string; // "HH:MM" exclusive; wraps past midnight
  readonly timezone: string; // IANA tz name, e.g. "Europe/Istanbul"
  readonly bypassSeverities?: ReadonlyArray<Severity>;
}

/** Per-adapter sliding-window business rate-limit policy. */
export interface RateLimitPolicy {
  readonly maxCount: number;
  readonly windowSeconds: number;
  readonly bypassSeverities?: ReadonlyArray<Severity>;
}

/**
 * Top-level config object passed to NotificationClient. All fields are
 * optional. `rateLimits` maps an adapter name to a policy; adapters not
 * present in the record are unconstrained.
 */
export interface NotificationConfig {
  readonly retry?: RetryConfig;
  readonly quietHours?: QuietHoursConfig;
  readonly rateLimits?: Readonly<Record<string, RateLimitPolicy>>;
}

export const DEFAULT_RETRY: RetryConfig = {
  maxAttempts: 3,
  baseDelayMs: 500,
  maxDelayMs: 10_000,
  jitterRatio: 0.2,
};

export const DEFAULT_BYPASS_SEVERITIES: ReadonlyArray<Severity> = [Severity.Critical];
