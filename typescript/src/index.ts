/**
 * @contriwork/notifications — TypeScript implementation.
 *
 * Public surface re-exported here mirrors CONTRACT.md. All other modules are
 * implementation detail and may change without a contract bump.
 */

export type { Adapter, AdapterDeliverResult } from "./adapter";
export { AdapterStatus } from "./adapter";
export {
  DiscordWebhookAdapter,
  type DiscordWebhookOptions,
  InMemoryAdapter,
  PushoverAdapter,
  type PushoverOptions,
  SlackWebhookAdapter,
  type SlackWebhookOptions,
  TelegramAdapter,
  type TelegramOptions,
} from "./adapters";
export { NotificationClient } from "./client";
export type {
  NotificationConfig,
  QuietHoursConfig,
  RateLimitPolicy,
  RetryConfig,
} from "./config";
export { DEFAULT_BYPASS_SEVERITIES, DEFAULT_RETRY } from "./config";
export { ErrorCode } from "./errors";
export type { Payload } from "./payload";
export type { NotificationPort } from "./port";
export type { AdapterOutcome, SendResult } from "./result";
export { OutcomeStatus } from "./result";
export { Severity, severityIcon } from "./severity";
