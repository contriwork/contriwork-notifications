# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).

Each release MUST contain three language sub-sections (`### Python`, `### C#`, `### npm`). The release workflow refuses to publish if any sub-section is missing.

## [Unreleased]

### Python

- _No changes yet._

### C#

- _No changes yet._

### npm

- _No changes yet._

## [0.1.0] - 2026-04-29

Initial release. Same `(severity, payload) → SendResult` API surface in all three languages, anchored to `CONTRACT.md` v1.

### Python

- `NotificationClient` multicast orchestrator with parallel delivery to a list of adapters.
- Adapters: `InMemoryAdapter`, `PushoverAdapter`, `TelegramAdapter`, `SlackWebhookAdapter`, `DiscordWebhookAdapter`, `SmtpAdapter`, plus `macos.IMessageAdapter` (macOS-only, opt-in import).
- 5-level `Severity` enum (`DEBUG`/`INFO`/`WARN`/`ERROR`/`CRITICAL`) with stable wire strings and icons.
- `Payload` validation: title ≤ 200 chars, body ≤ 2000 chars, optional `https://` url + `url_title` ≤ 100 chars.
- `RetryConfig` with exponential backoff + ±20% jitter (default `max_attempts=3`).
- `QuietHoursConfig` with TZ-aware `zoneinfo` evaluation and severity-based bypass.
- Per-adapter `RateLimitPolicy` (BUSINESS_RATE_LIMITED, distinct from upstream HTTP 429 RATE_LIMITED).
- Stable `ErrorCode` taxonomy for deterministic consumer-side branching.
- Tooling: Python ≥ 3.13, `uv` for env management, `pytest`, `ruff`, `mypy --strict`.

### C#

- `NotificationClient : INotificationPort` for the same multicast semantics.
- Adapters: `InMemoryAdapter`, `PushoverAdapter`, `TelegramAdapter`, `SlackWebhookAdapter`, `DiscordWebhookAdapter`, `SmtpAdapter` (MailKit 4.16.0, CVE-clean).
- AOT-friendly `System.Text.Json.Nodes` body construction (no source generators on the hot path).
- `IServiceCollection.AddContriworkNotifications(...)` extension for DI integration.
- TZ-aware quiet hours via `TimeZoneInfo`; ±20% jittered exponential backoff retry; per-adapter rate limiter.
- Targets `net10.0` (LTS); nullable enabled; `TreatWarningsAsErrors`; xUnit + WireMock.Net + NSubstitute test stack.

### npm

- `NotificationClient` with multicast `send` method matching the contract.
- Adapters: `InMemoryAdapter`, `PushoverAdapter`, `TelegramAdapter`, `SlackWebhookAdapter`, `DiscordWebhookAdapter`, `SmtpAdapter` (`nodemailer` 8.0.7).
- Pure-TS implementation using `globalThis.fetch` + `AbortController`; no native deps; tree-shakable (`sideEffects: false`).
- TZ-aware quiet hours via `Intl.DateTimeFormat`; jittered exponential backoff; per-adapter rate limiter.
- Dual-published ESM + CJS with `.d.ts` / `.d.cts`; npm provenance via OIDC.
- Targets Node.js ≥ 24; strict TS (`noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`); vitest test runner.

### Cross-language

- Shared contract-test fixture (`contract-tests/test_cases.json`, 13 cases) executed by all three language test runners; release is gated on all three being green.
- Tag-driven OIDC publish to PyPI, NuGet, and npm — all three registries publish at the same version on the same release.

[Unreleased]: https://github.com/contriwork/contriwork-notifications/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/contriwork/contriwork-notifications/releases/tag/v0.1.0
