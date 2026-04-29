import { describe, expect, it, vi } from "vitest";

import {
  AdapterStatus,
  ErrorCode,
  type Payload,
  PushoverAdapter,
  Severity,
} from "../src/index";

const SAMPLE_PAYLOAD: Payload = { title: "hi", body: "world" };
const API_URL = "https://api.pushover.net/1/messages.json";

function statusFetch(status: number): typeof fetch {
  return vi.fn(async () => new Response("", { status })) as unknown as typeof fetch;
}

function adapterWith(fetchImpl: typeof fetch): PushoverAdapter {
  return new PushoverAdapter({ userKey: "u", appToken: "t", fetchImpl });
}

describe("PushoverAdapter", () => {
  it("delivers on 2xx", async () => {
    const adapter = adapterWith(statusFetch(200));
    const result = await adapter.deliver(Severity.Info, SAMPLE_PAYLOAD, false);
    expect(result.status).toBe(AdapterStatus.Delivered);
  });

  it("auth_failed on 401", async () => {
    const result = await adapterWith(statusFetch(401)).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.PermanentFailure);
    expect(result.errorCode).toBe(ErrorCode.AuthFailed);
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

  it("upstream_error on 5xx", async () => {
    const result = await adapterWith(statusFetch(502)).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.RetriableFailure);
    expect(result.errorCode).toBe(ErrorCode.UpstreamError);
  });

  it("upstream_error on network failure", async () => {
    const fetchImpl = vi.fn(async () => {
      throw new Error("network down");
    }) as unknown as typeof fetch;
    const result = await adapterWith(fetchImpl).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.RetriableFailure);
    expect(result.errorCode).toBe(ErrorCode.UpstreamError);
  });

  it("silent forces priority -1 in the request body", async () => {
    const captured: { url?: string; body?: string } = {};
    const fetchImpl = vi.fn(async (url: unknown, init?: { body?: BodyInit | null }) => {
      captured.url = String(url);
      const body = init?.body;
      captured.body = body instanceof URLSearchParams ? body.toString() : String(body);
      return new Response("", { status: 200 });
    }) as unknown as typeof fetch;
    await adapterWith(fetchImpl).deliver(Severity.Warn, SAMPLE_PAYLOAD, true);
    expect(captured.url).toBe(API_URL);
    expect(captured.body).toContain("priority=-1");
  });

  it("isAvailable requires both creds", async () => {
    expect(
      await new PushoverAdapter({ userKey: "u", appToken: "t" }).isAvailable(),
    ).toBe(true);
    expect(
      await new PushoverAdapter({ userKey: "", appToken: "t" }).isAvailable(),
    ).toBe(false);
    expect(
      await new PushoverAdapter({ userKey: "u", appToken: "" }).isAvailable(),
    ).toBe(false);
  });
});
