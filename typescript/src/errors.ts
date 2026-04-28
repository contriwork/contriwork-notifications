/**
 * Stable error codes from CONTRACT.md §Error taxonomy. Codes are
 * SCREAMING_SNAKE_CASE strings, never renamed within v1; new codes may be
 * added but existing ones never change without a contract revision bump.
 */
export enum ErrorCode {
  // Client-level
  InvalidPayload = "INVALID_PAYLOAD",
  BusinessRateLimited = "BUSINESS_RATE_LIMITED",
  AllAdaptersFailed = "ALL_ADAPTERS_FAILED",

  // Adapter-level
  AdapterUnavailable = "ADAPTER_UNAVAILABLE",
  RateLimited = "RATE_LIMITED",
  AuthFailed = "AUTH_FAILED",
  AdapterInvalidPayload = "ADAPTER_INVALID_PAYLOAD",
  Timeout = "TIMEOUT",
  UpstreamError = "UPSTREAM_ERROR",
}
