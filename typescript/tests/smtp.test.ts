import { describe, expect, it, vi } from "vitest";

import {
  AdapterStatus,
  ErrorCode,
  type Payload,
  Severity,
  SmtpAdapter,
  type SmtpTransporter,
} from "../src/index";

const SAMPLE_PAYLOAD: Payload = { title: "hi", body: "world" };

function adapterWith(transporter: SmtpTransporter): SmtpAdapter {
  return new SmtpAdapter({
    host: "smtp.example.com",
    port: 587,
    fromAddr: "alerts@example.com",
    toAddrs: ["dest@example.com"],
    username: "alerts",
    password: "secret",
    transporter,
  });
}

describe("SmtpAdapter", () => {
  it("delivers when sendMail resolves", async () => {
    const sendMail = vi.fn(async () => ({}));
    const result = await adapterWith({ sendMail }).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.Delivered);
    expect(sendMail).toHaveBeenCalledOnce();
  });

  it("auth_failed on EAUTH", async () => {
    const sendMail = vi.fn(async () => {
      const err = new Error("bad creds") as Error & { code: string };
      err.code = "EAUTH";
      throw err;
    });
    const result = await adapterWith({ sendMail }).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.PermanentFailure);
    expect(result.errorCode).toBe(ErrorCode.AuthFailed);
  });

  it("invalid_payload on EENVELOPE", async () => {
    const sendMail = vi.fn(async () => {
      const err = new Error("bad envelope") as Error & { code: string };
      err.code = "EENVELOPE";
      throw err;
    });
    const result = await adapterWith({ sendMail }).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.PermanentFailure);
    expect(result.errorCode).toBe(ErrorCode.AdapterInvalidPayload);
  });

  it("timeout on ETIMEDOUT", async () => {
    const sendMail = vi.fn(async () => {
      const err = new Error("timed out") as Error & { code: string };
      err.code = "ETIMEDOUT";
      throw err;
    });
    const result = await adapterWith({ sendMail }).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.RetriableFailure);
    expect(result.errorCode).toBe(ErrorCode.Timeout);
  });

  it("upstream_error on generic failure", async () => {
    const sendMail = vi.fn(async () => {
      throw new Error("connection reset");
    });
    const result = await adapterWith({ sendMail }).deliver(
      Severity.Info,
      SAMPLE_PAYLOAD,
      false,
    );
    expect(result.status).toBe(AdapterStatus.RetriableFailure);
    expect(result.errorCode).toBe(ErrorCode.UpstreamError);
  });

  it("isAvailable requires required fields", async () => {
    const stub: SmtpTransporter = { sendMail: vi.fn(async () => ({})) };
    expect(await adapterWith(stub).isAvailable()).toBe(true);
    expect(
      await new SmtpAdapter({
        host: "",
        port: 587,
        fromAddr: "a@b.c",
        toAddrs: ["d@e.f"],
        transporter: stub,
      }).isAvailable(),
    ).toBe(false);
    expect(
      await new SmtpAdapter({
        host: "smtp.example.com",
        port: 587,
        fromAddr: "a@b.c",
        toAddrs: [],
        transporter: stub,
      }).isAvailable(),
    ).toBe(false);
  });
});
