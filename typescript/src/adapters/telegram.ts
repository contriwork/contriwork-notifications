import type { Adapter, AdapterDeliverResult } from "../adapter";
import { AdapterStatus } from "../adapter";
import { ErrorCode } from "../errors";
import type { Payload } from "../payload";
import type { Severity } from "../severity";

const DEFAULT_API_BASE = "https://api.telegram.org";
const DEFAULT_TIMEOUT_MS = 10_000;
const GENERIC_FAILURE = "Notification delivery failed";
const GENERIC_AUTH = "Authentication rejected";

export interface TelegramOptions {
  readonly botToken: string;
  readonly chatId: string;
  readonly name?: string;
  readonly apiBase?: string;
  readonly timeoutMs?: number;
  readonly fetchImpl?: typeof fetch;
}

/**
 * Telegram Bot API delivery via HTTPS POST to
 * `<apiBase>/bot<TOKEN>/sendMessage`. Plain-text body; severity is not
 * encoded in the wire payload. Silent mode forwards the Bot API
 * `disable_notification: true` flag.
 */
export class TelegramAdapter implements Adapter {
  readonly name: string;
  private readonly botToken: string;
  private readonly chatId: string;
  private readonly apiBase: string;
  private readonly timeoutMs: number;
  private readonly fetchImpl: typeof fetch;

  constructor(options: TelegramOptions) {
    this.botToken = options.botToken;
    this.chatId = options.chatId;
    this.name = options.name ?? "telegram";
    this.apiBase = options.apiBase ?? DEFAULT_API_BASE;
    this.timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;
    this.fetchImpl = options.fetchImpl ?? globalThis.fetch.bind(globalThis);
  }

  async isAvailable(): Promise<boolean> {
    return Boolean(this.botToken) && Boolean(this.chatId);
  }

  async deliver(
    _severity: Severity,
    payload: Payload,
    silent: boolean,
  ): Promise<AdapterDeliverResult> {
    const url = `${this.apiBase}/bot${this.botToken}/sendMessage`;
    let text = `${payload.title}\n\n${payload.body}`;
    if (payload.url !== undefined) {
      const label = payload.urlTitle ?? payload.url;
      text += `\n\n${label}: ${payload.url}`;
    }
    const body = JSON.stringify({
      chat_id: this.chatId,
      text,
      disable_notification: silent,
    });

    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);
    let response: Response;
    try {
      response = await this.fetchImpl(url, {
        method: "POST",
        headers: { "content-type": "application/json" },
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
