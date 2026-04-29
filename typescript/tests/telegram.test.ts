import { describe, expect, it, vi } from "vitest";

import {
  AdapterStatus,
  ErrorCode,
  type Payload,
  Severity,
  TelegramAdapter,
} from "../src/index";

const SAMPLE_PAYLOAD: Payload = { title: "hi", body: "world" };

function statusFetch(status: number, body = "{\"ok\":true}"): typeof fetch {
  return vi.fn(async () => new Response(body, { status })) as unknown as typeof fetch;
}

function adapterWith(fetchImpl: typeof fetch): TelegramAdapter {
  return new TelegramAdapter({ botToken: "BOT_TOKEN", chatId: "123", fetchImpl });
}

describe("TelegramAdapter", () => {
  it("delivers on 2xx", async () => {
    const result = await adapterWith(statusFetch(200)).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.Delivered);
  });

  it("auth_failed on 401", async () => {
    const result = await adapterWith(statusFetch(401, "")).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.PermanentFailure);
    expect(result.errorCode).toBe(ErrorCode.AuthFailed);
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

  it("upstream_error on 5xx", async () => {
    const result = await adapterWith(statusFetch(503, "")).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.RetriableFailure);
    expect(result.errorCode).toBe(ErrorCode.UpstreamError);
  });

  it("silent sets disable_notification true", async () => {
    let capturedBody: string | undefined;
    const fetchImpl = vi.fn(async (_url: unknown, init?: { body?: BodyInit | null }) => {
      capturedBody = String(init?.body ?? "");
      return new Response("{\"ok\":true}", { status: 200 });
    }) as unknown as typeof fetch;
    await adapterWith(fetchImpl).deliver(Severity.Warn, SAMPLE_PAYLOAD, true);
    const body = JSON.parse(capturedBody ?? "{}") as { disable_notification: boolean };
    expect(body.disable_notification).toBe(true);
  });

  it("appends url to text", async () => {
    let capturedBody: string | undefined;
    const fetchImpl = vi.fn(async (_url: unknown, init?: { body?: BodyInit | null }) => {
      capturedBody = String(init?.body ?? "");
      return new Response("{\"ok\":true}", { status: 200 });
    }) as unknown as typeof fetch;
    await adapterWith(fetchImpl).deliver(
      Severity.Info,
      { title: "t", body: "b", url: "https://example.com", urlTitle: "link" },
      false,
    );
    const body = JSON.parse(capturedBody ?? "{}") as { text: string };
    expect(body.text).toContain("link: https://example.com");
  });
});
