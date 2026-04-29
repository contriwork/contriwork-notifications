import type { Adapter, AdapterDeliverResult } from "../adapter";
import { AdapterStatus } from "../adapter";
import { ErrorCode } from "../errors";
import type { Payload } from "../payload";
import type { Severity } from "../severity";

const DEFAULT_TIMEOUT_MS = 10_000;
const SUPPRESS_NOTIFICATIONS_FLAG = 4096;
const GENERIC_FAILURE = "Notification delivery failed";
const GENERIC_AUTH = "Authentication rejected";

export interface DiscordWebhookOptions {
  readonly webhookUrl: string;
  readonly name?: string;
  readonly timeoutMs?: number;
  readonly fetchImpl?: typeof fetch;
}

/**
 * Discord webhook delivery. Plain Markdown content (`**title**` + body,
 * optional `[label](url)` link). Silent mode sets the message `flags`
 * field to 4096 (`SUPPRESS_NOTIFICATIONS`).
 */
export class DiscordWebhookAdapter implements Adapter {
  readonly name: string;
  private readonly webhookUrl: string;
  private readonly timeoutMs: number;
  private readonly fetchImpl: typeof fetch;

  constructor(options: DiscordWebhookOptions) {
    this.webhookUrl = options.webhookUrl;
    this.name = options.name ?? "discord-webhook";
    this.timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;
    this.fetchImpl = options.fetchImpl ?? globalThis.fetch.bind(globalThis);
  }

  async isAvailable(): Promise<boolean> {
    return Boolean(this.webhookUrl);
  }

  async deliver(
    _severity: Severity,
    payload: Payload,
    silent: boolean,
  ): Promise<AdapterDeliverResult> {
    let content = `**${payload.title}**\n${payload.body}`;
    if (payload.url !== undefined) {
      const label = payload.urlTitle ?? payload.url;
      content += `\n[${label}](${payload.url})`;
    }
    const bodyObj: Record<string, unknown> = { content };
    if (silent) {
      bodyObj["flags"] = SUPPRESS_NOTIFICATIONS_FLAG;
    }
    const body = JSON.stringify(bodyObj);

    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);
    let response: Response;
    try {
      response = await this.fetchImpl(this.webhookUrl, {
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
  // Discord webhook returns 204 No Content on success; treat any 2xx.
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
