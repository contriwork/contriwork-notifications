import type { Payload } from "./payload";
import type { SendResult } from "./result";
import type { Severity } from "./severity";

/**
 * Transport-only notification port. CONTRACT.md §Port.
 * The concrete implementation is `NotificationClient`; consumers may type
 * against this interface to substitute the implementation in tests.
 */
export interface NotificationPort {
  /**
   * Multicast the payload to every configured adapter in parallel.
   * See CONTRACT.md §Methods → `send` for the full semantics.
   */
  send(severity: Severity, payload: Payload): Promise<SendResult>;
}
