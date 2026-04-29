import { describe, expect, it, vi } from "vitest";

import {
  AdapterStatus,
  ErrorCode,
  type Payload,
  Severity,
  SlackWebhookAdapter,
} from "../src/index";

const SAMPLE_PAYLOAD: Payload = { title: "hi", body: "world" };
const WEBHOOK = "https://hooks.slack.com/services/T0/B0/secret";

function statusFetch(status: number, body = "ok"): typeof fetch {
  return vi.fn(async () => new Response(body, { status })) as unknown as typeof fetch;
}

function adapterWith(fetchImpl: typeof fetch): SlackWebhookAdapter {
  return new SlackWebhookAdapter({ webhookUrl: WEBHOOK, fetchImpl });
}

describe("SlackWebhookAdapter", () => {
  it("delivers on 2xx", async () => {
    const result = await adapterWith(statusFetch(200)).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.Delivered);
  });

  it("invalid_payload on 400", async () => {
    const result = await adapterWith(statusFetch(400, "")).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.PermanentFailure);
    expect(result.errorCode).toBe(ErrorCode.AdapterInvalidPayload);
  });

  it("rate_limited on 429", async () => {
    const result = await adapterWith(statusFetch(429, "")).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.RetriableFailure);
    expect(result.errorCode).toBe(ErrorCode.RateLimited);
  });

  it("upstream_error on 500", async () => {
    const result = await adapterWith(statusFetch(500, "")).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.RetriableFailure);
    expect(result.errorCode).toBe(ErrorCode.UpstreamError);
  });

  it("silent is a no-op in the payload", async () => {
    let capturedBody: string | undefined;
    const fetchImpl = vi.fn(async (_url: unknown, init?: { body?: BodyInit | null }) => {
      capturedBody = String(init?.body ?? "");
      return new Response("ok", { status: 200 });
    }) as unknown as typeof fetch;
    await adapterWith(fetchImpl).deliver(Severity.Info, SAMPLE_PAYLOAD, true);
    const body = capturedBody ?? "";
    // No native silent flag; payload is identical regardless of silent.
    expect(body).not.toContain("flags");
    expect(body).not.toContain("disable_notification");
  });
});
