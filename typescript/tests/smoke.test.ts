import { describe, expect, it } from "vitest";
import {
  AdapterStatus,
  ErrorCode,
  NotificationClient,
  OutcomeStatus,
  Severity,
  severityIcon,
} from "../src/index";

describe("smoke", () => {
  it("Severity values are stable wire strings", () => {
    expect(Severity.Debug).toBe("DEBUG");
    expect(Severity.Info).toBe("INFO");
    expect(Severity.Warn).toBe("WARN");
    expect(Severity.Error).toBe("ERROR");
    expect(Severity.Critical).toBe("CRITICAL");
  });

  it("severity icons are stable", () => {
    expect(severityIcon(Severity.Debug)).toBe("🔍");
    expect(severityIcon(Severity.Info)).toBe("ℹ️");
    expect(severityIcon(Severity.Warn)).toBe("⚠️");
    expect(severityIcon(Severity.Error)).toBe("❌");
    expect(severityIcon(Severity.Critical)).toBe("⛔");
  });

  it("OutcomeStatus values are stable", () => {
    expect(OutcomeStatus.Delivered).toBe("DELIVERED");
    expect(OutcomeStatus.DeliveredSilent).toBe("DELIVERED_SILENT");
    expect(OutcomeStatus.RetriableFailure).toBe("RETRIABLE_FAILURE");
    expect(OutcomeStatus.PermanentFailure).toBe("PERMANENT_FAILURE");
    expect(OutcomeStatus.BusinessRateLimited).toBe("BUSINESS_RATE_LIMITED");
  });

  it("AdapterStatus values are stable", () => {
    expect(AdapterStatus.Delivered).toBe("DELIVERED");
    expect(AdapterStatus.RetriableFailure).toBe("RETRIABLE_FAILURE");
    expect(AdapterStatus.PermanentFailure).toBe("PERMANENT_FAILURE");
  });

  it("ErrorCode values are stable", () => {
    expect(ErrorCode.InvalidPayload).toBe("INVALID_PAYLOAD");
    expect(ErrorCode.BusinessRateLimited).toBe("BUSINESS_RATE_LIMITED");
    expect(ErrorCode.AllAdaptersFailed).toBe("ALL_ADAPTERS_FAILED");
    expect(ErrorCode.AdapterUnavailable).toBe("ADAPTER_UNAVAILABLE");
    expect(ErrorCode.RateLimited).toBe("RATE_LIMITED");
    expect(ErrorCode.AuthFailed).toBe("AUTH_FAILED");
    expect(ErrorCode.AdapterInvalidPayload).toBe("ADAPTER_INVALID_PAYLOAD");
    expect(ErrorCode.Timeout).toBe("TIMEOUT");
    expect(ErrorCode.UpstreamError).toBe("UPSTREAM_ERROR");
  });

  it("empty adapter list send is noop", async () => {
    const client = new NotificationClient([]);
    const result = await client.send(Severity.Info, { title: "hi", body: "world" });
    expect(result.ok).toBe(true);
    expect(result.results).toHaveLength(0);
    expect(result.errorCode).toBeUndefined();
    expect(result.attempts).toBe(0);
  });

  it("invalid payload short-circuits with INVALID_PAYLOAD", async () => {
    const client = new NotificationClient([]);
    const oversize = "A".repeat(201);
    const result = await client.send(Severity.Info, { title: oversize, body: "ok" });
    expect(result.ok).toBe(false);
    expect(result.errorCode).toBe(ErrorCode.InvalidPayload);
  });

  it("non-https url short-circuits with INVALID_PAYLOAD", async () => {
    const client = new NotificationClient([]);
    const result = await client.send(Severity.Info, {
      title: "hi",
      body: "ok",
      url: "http://example.com",
    });
    expect(result.ok).toBe(false);
    expect(result.errorCode).toBe(ErrorCode.InvalidPayload);
  });
});
