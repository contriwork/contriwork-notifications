import type { Adapter, AdapterDeliverResult } from "../adapter";
import { AdapterStatus } from "../adapter";
import { ErrorCode } from "../errors";
import type { Payload } from "../payload";
import type { Severity } from "../severity";

const DEFAULT_TIMEOUT_MS = 10_000;
const GENERIC_FAILURE = "Notification delivery failed";
const GENERIC_AUTH = "Authentication rejected";

export interface SlackWebhookOptions {
  readonly webhookUrl: string;
  readonly name?: string;
  readonly timeoutMs?: number;
  readonly fetchImpl?: typeof fetch;
}

/**
 * Slack incoming-webhook delivery. Plain mrkdwn body (`*title*` + body,
 * optional `<url|label>` link). Slack incoming webhooks have no native
 * silent flag; `silent` is a documented no-op.
 */
export class SlackWebhookAdapter implements Adapter {
  readonly name: string;
  private readonly webhookUrl: string;
  private readonly timeoutMs: number;
  private readonly fetchImpl: typeof fetch;

  constructor(options: SlackWebhookOptions) {
    this.webhookUrl = options.webhookUrl;
    this.name = options.name ?? "slack-webhook";
    this.timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;
    this.fetchImpl = options.fetchImpl ?? globalThis.fetch.bind(globalThis);
  }

  async isAvailable(): Promise<boolean> {
    return Boolean(this.webhookUrl);
  }

  async deliver(
    _severity: Severity,
    payload: Payload,
    _silent: boolean,
  ): Promise<AdapterDeliverResult> {
    let text = `*${payload.title}*\n${payload.body}`;
    if (payload.url !== undefined) {
      const label = payload.urlTitle ?? payload.url;
      text += `\n<${payload.url}|${label}>`;
    }
    const body = JSON.stringify({ text });

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
