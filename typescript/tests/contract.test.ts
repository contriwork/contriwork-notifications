import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

import {
  type AdapterDeliverResult,
  AdapterStatus,
  type AdapterOutcome,
  ErrorCode,
  InMemoryAdapter,
  NotificationClient,
  type NotificationConfig,
  OutcomeStatus,
  type Payload,
  type QuietHoursConfig,
  type RateLimitPolicy,
  type RetryConfig,
  type Severity,
} from "../src/index";

/**
 * Cross-language contract test runner. Loads
 * contract-tests/test_cases.json from the repo root and asserts each
 * fixture's expected_output against the result NotificationClient
 * produces when driven with InMemoryAdapter instances built from the
 * per-fixture behavior sequence. The same JSON drives the Python and
 * C# runners; if a behavior here changes, all three runners change in
 * the same PR.
 */

interface BehaviorEntry {
  status: string;
  error_code?: string | null;
  detail?: string | null;
  silent_observed?: boolean;
}

interface AdapterSpec {
  name: string;
  behavior?: { sequence?: BehaviorEntry[] };
}

interface QuietSpec {
  start: string;
  end: string;
  timezone: string;
  bypass_severities: string[];
}

interface RateSpec {
  max_count: number;
  window_seconds: number;
  bypass_severities: string[];
}

interface RetrySpec {
  max_attempts: number;
  base_delay_ms: number;
  max_delay_ms: number;
  jitter_ratio: number;
}

interface ConfigSpec {
  retry?: RetrySpec | null;
  quiet_hours?: QuietSpec | null;
  rate_limits?: Record<string, RateSpec> | null;
}

interface PayloadSpec {
  title: string;
  body: string;
  url?: string | null;
  url_title?: string | null;
  metadata?: Record<string, string> | null;
}

interface CaseInput {
  adapters: AdapterSpec[];
  config: ConfigSpec;
  send: { severity: string; payload: PayloadSpec };
}

interface ResultSpec {
  adapter: string;
  status: string;
  attempts: number;
  error_code?: string | null;
}

interface ExpectedOutput {
  ok: boolean;
  error_code?: string | null;
  attempts: number;
  results: ResultSpec[];
}

interface ContractCase {
  name: string;
  description?: string;
  input: CaseInput;
  expected_output: ExpectedOutput;
  skip_languages?: string[];
}

interface Fixture {
  schema_version: number;
  contract_revision: string;
  cases: ContractCase[];
}

const here = fileURLToPath(new URL(".", import.meta.url));
const fixturePath = resolve(here, "../../contract-tests/test_cases.json");
const fixture = JSON.parse(readFileSync(fixturePath, "utf-8")) as Fixture;

describe("contract fixture", () => {
  it("is well-formed", () => {
    expect(fixture.schema_version).toBe(1);
    expect(fixture.contract_revision).toBe("v1");
    expect(Array.isArray(fixture.cases)).toBe(true);
    expect(fixture.cases.length).toBeGreaterThan(0);
  });
});

function buildAdapter(spec: AdapterSpec): InMemoryAdapter {
  const sequence: AdapterDeliverResult[] = (spec.behavior?.sequence ?? []).map((entry) => {
    const status = entry.status as AdapterStatus;
    const out: { status: AdapterStatus; errorCode?: ErrorCode } = { status };
    if (entry.error_code != null) {
      out.errorCode = entry.error_code as ErrorCode;
    }
    return out;
  });
  return new InMemoryAdapter(spec.name, sequence);
}

function buildConfig(spec: ConfigSpec): NotificationConfig {
  const config: { retry?: RetryConfig; quietHours?: QuietHoursConfig; rateLimits?: Record<string, RateLimitPolicy> } = {};

  if (spec.retry) {
    config.retry = {
      maxAttempts: spec.retry.max_attempts,
      baseDelayMs: spec.retry.base_delay_ms,
      maxDelayMs: spec.retry.max_delay_ms,
      jitterRatio: spec.retry.jitter_ratio,
    };
  }

  if (spec.quiet_hours) {
    config.quietHours = {
      start: spec.quiet_hours.start,
      end: spec.quiet_hours.end,
      timezone: spec.quiet_hours.timezone,
      bypassSeverities: spec.quiet_hours.bypass_severities as Severity[],
    };
  }

  if (spec.rate_limits) {
    const rates: Record<string, RateLimitPolicy> = {};
    for (const [name, p] of Object.entries(spec.rate_limits)) {
      rates[name] = {
        maxCount: p.max_count,
        windowSeconds: p.window_seconds,
        bypassSeverities: p.bypass_severities as Severity[],
      };
    }
    config.rateLimits = rates;
  }

  return config;
}

function buildPayload(spec: PayloadSpec): Payload {
  const out: { title: string; body: string; url?: string; urlTitle?: string } = {
    title: spec.title,
    body: spec.body,
  };
  if (spec.url != null) out.url = spec.url;
  if (spec.url_title != null) out.urlTitle = spec.url_title;
  return out;
}

const runnable = fixture.cases.filter(
  (c) => !(c.skip_languages ?? []).includes("typescript"),
);

describe.each(runnable)("contract case $name", (c) => {
  it("matches expected_output", async () => {
    const adapters = c.input.adapters.map(buildAdapter);
    const config = buildConfig(c.input.config);
    const client = new NotificationClient(adapters, config);
    const severity = c.input.send.severity as Severity;
    const payload = buildPayload(c.input.send.payload);

    const result = await client.send(severity, payload);
    const expected = c.expected_output;

    expect(result.ok).toBe(expected.ok);
    if (expected.error_code == null) {
      expect(result.errorCode).toBeUndefined();
    } else {
      expect(result.errorCode).toBe(expected.error_code);
    }
    expect(result.attempts).toBe(expected.attempts);
    expect(result.results).toHaveLength(expected.results.length);

    expected.results.forEach((exp: ResultSpec, i: number) => {
      const actual = result.results[i] as AdapterOutcome;
      expect(actual.adapter).toBe(exp.adapter);
      expect(actual.status).toBe(exp.status as OutcomeStatus);
      expect(actual.attempts).toBe(exp.attempts);
      if (exp.error_code == null) {
        expect(actual.errorCode).toBeUndefined();
      } else {
        expect(actual.errorCode).toBe(exp.error_code);
      }
    });
  });
});
