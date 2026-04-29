import nodemailer, { type SendMailOptions, type Transporter } from "nodemailer";

import type { Adapter, AdapterDeliverResult } from "../adapter";
import { AdapterStatus } from "../adapter";
import { ErrorCode } from "../errors";
import type { Payload } from "../payload";
import type { Severity } from "../severity";

const DEFAULT_TIMEOUT_MS = 30_000;
const GENERIC_FAILURE = "Notification delivery failed";
const GENERIC_AUTH = "Authentication rejected";

/**
 * Minimal subset of `nodemailer.Transporter` the adapter actually uses.
 * Tests inject a fake conforming to this shape; real production code uses
 * `nodemailer.createTransport(...)` which returns a wider Transporter.
 */
export interface SmtpTransporter {
  sendMail(options: SendMailOptions): Promise<unknown>;
}

export interface SmtpOptions {
  readonly host: string;
  readonly port: number;
  readonly fromAddr: string;
  readonly toAddrs: ReadonlyArray<string>;
  readonly username?: string;
  readonly password?: string;
  /** Implicit TLS (default false; use port 465 with this set to true). */
  readonly useTls?: boolean;
  /** STARTTLS upgrade on a plaintext connection (default true; port 587). */
  readonly startTls?: boolean;
  readonly name?: string;
  readonly timeoutMs?: number;
  /**
   * Optional custom transporter (for unit tests). Production code leaves
   * this undefined and the adapter creates its own via
   * `nodemailer.createTransport`.
   */
  readonly transporter?: SmtpTransporter;
}

interface NodemailerError {
  readonly code?: string;
  readonly message?: string;
}

/**
 * SMTP delivery via nodemailer. Default transport is STARTTLS on port 587;
 * pass `useTls: true` + `startTls: false` for implicit TLS (port 465).
 *
 * Email has no programmatic silent flag; `silent` is a documented no-op
 * and the wire payload is identical regardless. The orchestrator still
 * reports DELIVERED_SILENT for accounting.
 */
export class SmtpAdapter implements Adapter {
  readonly name: string;
  private readonly fromAddr: string;
  private readonly toAddrs: ReadonlyArray<string>;
  private readonly transporter: SmtpTransporter;
  private readonly timeoutMs: number;

  constructor(options: SmtpOptions) {
    this.name = options.name ?? "smtp";
    this.fromAddr = options.fromAddr;
    this.toAddrs = [...options.toAddrs];
    this.timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;

    if (options.transporter !== undefined) {
      this.transporter = options.transporter;
    } else {
      const transport: Transporter = nodemailer.createTransport({
        host: options.host,
        port: options.port,
        secure: options.useTls ?? false,
        requireTLS: options.startTls ?? true,
        connectionTimeout: this.timeoutMs,
        greetingTimeout: this.timeoutMs,
        socketTimeout: this.timeoutMs,
        ...(options.username !== undefined && options.password !== undefined
          ? { auth: { user: options.username, pass: options.password } }
          : {}),
      });
      this.transporter = transport;
    }
    // Validate required fields after capturing them so isAvailable can answer.
    this.requiredFieldsOk =
      Boolean(options.host) &&
      options.port > 0 &&
      Boolean(options.fromAddr) &&
      this.toAddrs.length > 0;
  }

  private readonly requiredFieldsOk: boolean;

  async isAvailable(): Promise<boolean> {
    return this.requiredFieldsOk;
  }

  async deliver(
    _severity: Severity,
    payload: Payload,
    _silent: boolean,
  ): Promise<AdapterDeliverResult> {
    let body = payload.body;
    if (payload.url !== undefined) {
      const label = payload.urlTitle ?? payload.url;
      body += `\n\n${label}: ${payload.url}`;
    }

    try {
      await this.transporter.sendMail({
        from: this.fromAddr,
        to: this.toAddrs.join(", "),
        subject: payload.title,
        text: body,
      });
    } catch (err) {
      const code = (err as NodemailerError | undefined)?.code;
      if (code === "EAUTH") {
        return {
          status: AdapterStatus.PermanentFailure,
          errorCode: ErrorCode.AuthFailed,
          detail: GENERIC_AUTH,
        };
      }
      if (code === "EENVELOPE") {
        return {
          status: AdapterStatus.PermanentFailure,
          errorCode: ErrorCode.AdapterInvalidPayload,
          detail: GENERIC_FAILURE,
        };
      }
      if (code === "ETIMEDOUT") {
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
    }

    return { status: AdapterStatus.Delivered };
  }
}
