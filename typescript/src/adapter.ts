import type { ErrorCode } from "./errors";
import type { Payload } from "./payload";
import type { Severity } from "./severity";

/** Outcome of a single Adapter.deliver call (no retry semantics). */
export enum AdapterStatus {
  Delivered = "DELIVERED",
  RetriableFailure = "RETRIABLE_FAILURE",
  PermanentFailure = "PERMANENT_FAILURE",
}

/** Result of one Adapter.deliver invocation. */
export interface AdapterDeliverResult {
  readonly status: AdapterStatus;
  readonly errorCode?: ErrorCode;
  readonly detail?: string;
}

/**
 * Adapter contract. CONTRACT.md §Adapter protocol.
 *
 * Implementations MUST:
 *  - expose a stable string `name` (e.g. "pushover", "slack-webhook")
 *  - return cheaply from `isAvailable` (no network in the common case)
 *  - translate `silent === true` into the channel's nearest equivalent silent
 *    representation, or document a no-op explicitly
 */
export interface Adapter {
  readonly name: string;
  isAvailable(): Promise<boolean>;
  deliver(severity: Severity, payload: Payload, silent: boolean): Promise<AdapterDeliverResult>;
}
