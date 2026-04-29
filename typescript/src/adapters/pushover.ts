import type { Adapter, AdapterDeliverResult } from "../adapter";
import { AdapterStatus } from "../adapter";
import { ErrorCode } from "../errors";
import type { Payload } from "../payload";
import { Severity } from "../severity";

const DEFAULT_API_URL = "https://api.pushover.net/1/messages.json";
const DEFAULT_TIMEOUT_MS = 10_000;
const GENERIC_FAILURE = "Notification delivery failed";
const GENERIC_AUTH = "Authentication rejected";

export interface PushoverOptions {
  readonly userKey: string;
  readonly appToken: string;
  readonly name?: string;
  readonly errorPriority?: 0 | 1;
  readonly apiUrl?: string;
  readonly timeoutMs?: number;
  readonly fetchImpl?: typeof fetch;
}

/**
 * Pushover delivery via HTTPS POST to api.pushover.net/1/messages.json.
 *
 * Severity → Pushover priority mapping:
 *   - Debug    → -1 (silent / badge-only)
 *   - Info     →  0
 *   - Warn     →  0
 *   - Error    →  errorPriority (default 0; pass 1 to escalate)
 *   - Critical →  1 (high priority)
 *
 * Silent mode: priority forced to -1.
 */
export class PushoverAdapter implements Adapter {
  readonly name: string;
  private readonly userKey: string;
  private readonly appToken: string;
  private readonly errorPriority: 0 | 1;
  private readonly apiUrl: string;
  private readonly timeoutMs: number;
  private readonly fetchImpl: typeof fetch;

  constructor(options: PushoverOptions) {
    this.userKey = options.userKey;
    this.appToken = options.appToken;
    this.name = options.name ?? "pushover";
    this.errorPriority = options.errorPriority ?? 0;
    this.apiUrl = options.apiUrl ?? DEFAULT_API_URL;
    this.timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;
    this.fetchImpl = options.fetchImpl ?? globalThis.fetch.bind(globalThis);
  }

  async isAvailable(): Promise<boolean> {
    return Boolean(this.userKey) && Boolean(this.appToken);
  }

  async deliver(
    severity: Severity,
    payload: Payload,
    silent: boolean,
  ): Promise<AdapterDeliverResult> {
    const priority = silent ? -1 : this.priorityFor(severity);
    const body = new URLSearchParams();
    body.set("token", this.appToken);
    body.set("user", this.userKey);
    body.set("title", payload.title);
    body.set("message", payload.body);
    body.set("priority", String(priority));
    if (payload.url !== undefined) body.set("url", payload.url);
    if (payload.urlTitle !== undefined) body.set("url_title", payload.urlTitle);

    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);
    let response: Response;
    try {
      response = await this.fetchImpl(this.apiUrl, {
        method: "POST",
        body,
        signal: controller.signal,
      });
    } catch {
      if (controller.signal.aborted) {
        return {
          status: AdapterStatus.RetriableFailure,
          errorCode: ErrorCode.Timeout,
          detail: GENERIC_FAILURE,
        };
      }
      return {
        status: AdapterStatus.RetriableFailure,
        errorCode: ErrorCode.UpstreamError,
        detail: GENERIC_FAILURE,
      };
    } finally {
      clearTimeout(timer);
    }

    return mapResponse(response.status);
  }

  private priorityFor(severity: Severity): number {
    if (severity === Severity.Debug) return -1;
    if (severity === Severity.Critical) return 1;
    if (severity === Severity.Error) return this.errorPriority;
    return 0;
  }
}

function mapResponse(statusCode: number): AdapterDeliverResult {
  if (statusCode >= 200 && statusCode < 300) {
    return { status: AdapterStatus.Delivered };
  }
  if (statusCode === 429) {
    return {
      status: AdapterStatus.RetriableFailure,
      errorCode: ErrorCode.RateLimited,
      detail: GENERIC_FAILURE,
    };
  }
  if (statusCode === 401 || statusCode === 403) {
    return {
      status: AdapterStatus.PermanentFailure,
      errorCode: ErrorCode.AuthFailed,
      detail: GENERIC_AUTH,
    };
  }
  if (statusCode >= 400 && statusCode < 500) {
    return {
      status: AdapterStatus.PermanentFailure,
      errorCode: ErrorCode.AdapterInvalidPayload,
      detail: GENERIC_FAILURE,
    };
  }
  return {
    status: AdapterStatus.RetriableFailure,
    errorCode: ErrorCode.UpstreamError,
    detail: GENERIC_FAILURE,
  };
}
