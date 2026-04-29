import { describe, expect, it, vi } from "vitest";

import {
  AdapterStatus,
  DiscordWebhookAdapter,
  ErrorCode,
  type Payload,
  Severity,
} from "../src/index";

const SAMPLE_PAYLOAD: Payload = { title: "hi", body: "world" };
const WEBHOOK = "https://discord.com/api/webhooks/123/secret";

function statusFetch(status: number): typeof fetch {
  // 204 No Content cannot carry a body; pass null so runtime accepts it.
  return vi.fn(async () => new Response(null, { status })) as unknown as typeof fetch;
}

function adapterWith(fetchImpl: typeof fetch): DiscordWebhookAdapter {
  return new DiscordWebhookAdapter({ webhookUrl: WEBHOOK, fetchImpl });
}

describe("DiscordWebhookAdapter", () => {
  it("delivers on 204", async () => {
    const result = await adapterWith(statusFetch(204)).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.Delivered);
  });

  it("invalid_payload on 400", async () => {
    const result = await adapterWith(statusFetch(400)).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.PermanentFailure);
    expect(result.errorCode).toBe(ErrorCode.AdapterInvalidPayload);
  });

  it("rate_limited on 429", async () => {
    const result = await adapterWith(statusFetch(429)).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.RetriableFailure);
    expect(result.errorCode).toBe(ErrorCode.RateLimited);
  });

  it("upstream_error on 500", async () => {
    const result = await adapterWith(statusFetch(500)).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.RetriableFailure);
    expect(result.errorCode).toBe(ErrorCode.UpstreamError);
  });

  it("silent sets flags=4096", async () => {
    let capturedBody: string | undefined;
    const fetchImpl = vi.fn(async (_url: unknown, init?: { body?: BodyInit | null }) => {
      capturedBody = String(init?.body ?? "");
      return new Response("", { status: 204 });
    }) as unknown as typeof fetch;
    await adapterWith(fetchImpl).deliver(Severity.Info, SAMPLE_PAYLOAD, true);
    const body = JSON.parse(capturedBody ?? "{}") as { flags?: number };
    expect(body.flags).toBe(4096);
  });

  it("non-silent omits flags", async () => {
    let capturedBody: string | undefined;
    const fetchImpl = vi.fn(async (_url: unknown, init?: { body?: BodyInit | null }) => {
      capturedBody = String(init?.body ?? "");
      return new Response("", { status: 204 });
    }) as unknown as typeof fetch;
    await adapterWith(fetchImpl).deliver(Severity.Info, SAMPLE_PAYLOAD, false);
    expect(capturedBody ?? "").not.toContain("\"flags\"");
  });
});
