import type { ErrorCode } from "./errors";

/** Per-adapter outcome status reported by the orchestrator. */
export enum OutcomeStatus {
  Delivered = "DELIVERED",
  DeliveredSilent = "DELIVERED_SILENT",
  RetriableFailure = "RETRIABLE_FAILURE",
  PermanentFailure = "PERMANENT_FAILURE",
  BusinessRateLimited = "BUSINESS_RATE_LIMITED",
}

/** Aggregate outcome reported per adapter, after retries. */
export interface AdapterOutcome {
  readonly adapter: string;
  readonly status: OutcomeStatus;
  readonly attempts: number;
  readonly errorCode?: ErrorCode;
  readonly detail?: string;
}

/** Result of a single NotificationClient.send call. */
export interface SendResult {
  readonly ok: boolean;
  readonly results: ReadonlyArray<AdapterOutcome>;
  readonly errorCode?: ErrorCode;
  readonly attempts: number;
}
