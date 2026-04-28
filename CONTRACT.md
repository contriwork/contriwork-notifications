# CONTRACT — Notifications

This document is the **language-agnostic contract** for `contriwork-notifications`.
It is the single source of truth for the public surface across Python, C#, and
TypeScript implementations. Every change to public behavior MUST start here
before any code is written in `python/`, `csharp/`, or `typescript/`.

Contract revision (bumped on any behavior-visible change): **v1**

---

## Overview

`contriwork-notifications` is a **transport-only** abstraction for delivering
short messages to humans across multiple outbound channels (Pushover, Telegram,
Slack webhook, Discord webhook, SMTP, plus Python-only iMessage). The package
takes a list of pre-configured adapter instances and a `(severity, payload)`
pair, and delivers the payload to **every** adapter in parallel (multicast).
Failures are reported per-adapter; the call fails as a whole only when **all**
adapters fail with permanent errors.

The package is intentionally minimal:

- It **does not** persist messages, deduplicate, route by named channels,
  manage adapter lifecycles, or own configuration sources.
- It **does** validate input, enforce business-level rate limits, downgrade
  notifications to silent mode during configured quiet hours, and retry
  retriable failures with exponential backoff and jitter.

Anything beyond that — channel-name routing, in-app DB persistence,
deduplication, fan-out from semantic events to adapter sets — is the
**consumer's** responsibility.

---

## Port (language-agnostic interface)

The port defines the operations exposed across all three language
implementations. Method names MUST be identical modulo language-idiomatic
casing (`snake_case` for Python, `PascalCaseAsync` for C#, `camelCase` for
TypeScript).

| Operation | Input                                              | Output         | Failure modes                                                |
|-----------|----------------------------------------------------|----------------|--------------------------------------------------------------|
| `send`    | `severity: Severity`, `payload: Payload`           | `SendResult`   | `INVALID_PAYLOAD`, `BUSINESS_RATE_LIMITED`, `ALL_ADAPTERS_FAILED` |

The client is constructed with an explicit list of adapter instances:

```
NotificationClient(adapters: list[Adapter], config: NotificationConfig | None)
```

The client does not discover adapters. The consumer instantiates each adapter
with its own credentials and passes the list in. An empty `adapters` list is
permitted; `send` becomes a no-op and returns `SendResult{ok: true, results: []}`.

---

## Methods

### `send`

- **Signature**: `send(severity: Severity, payload: Payload) -> SendResult`
- **Parameters**:
  - `severity` — `Severity` enum value. See [Severity](#severity).
  - `payload` — `Payload` object. See [Payload schema](#payload-schema).
- **Returns**: `SendResult` (see [Send result](#send-result)). Success when at
  least one adapter delivered (including silent mode); failure when all
  adapters returned permanent errors or business rate limits.
- **Preconditions**: `payload` MUST satisfy the validation rules in
  [Payload schema](#payload-schema). Adapters MUST be initialized.
- **Postconditions**: every adapter in the list has been invoked at most
  `retry.max_attempts` times; the result enumerates per-adapter outcomes in
  the same order as the adapter list.

---

## Severity

Five severities, fixed order, never renamed:

| Severity   | Icon | Meaning                                       | Typical Pushover priority (adapter default) |
|------------|------|-----------------------------------------------|----------------------------------------------|
| `DEBUG`    | 🔍   | Diagnostic; silent by design                  | -1                                           |
| `INFO`     | ℹ️   | Informational                                 |  0                                           |
| `WARN`     | ⚠️   | Recoverable issue                             |  0                                           |
| `ERROR`    | ❌   | Failure that may need attention               |  0 (configurable up to 1)                    |
| `CRITICAL` | ⛔   | Urgent; bypasses quiet hours by default       |  1 (with adapter-specific escalation)        |

The icon is exposed as `Severity.icon` (read-only). Title-prefixing is the
**adapter's** decision; the port does not mutate `payload.title`.

The Pushover priority column is the **default mapping** of the bundled Pushover
adapter; other adapters apply analogous mappings (e.g., Telegram
`disable_notification`, Discord `SUPPRESS_NOTIFICATIONS` flag, Slack no-op).

---

## Payload schema

| Field      | Type                  | Required | Constraint                                      |
|------------|-----------------------|----------|-------------------------------------------------|
| `title`    | string                | yes      | non-empty, ≤ 200 chars (after trim), UTF-8      |
| `body`     | string                | yes      | non-empty, ≤ 2000 chars (after trim), UTF-8     |
| `url`      | string \| null        | no       | if present, MUST start with `https://`          |
| `url_title`| string \| null        | no       | ≤ 100 chars; ignored if `url` is null           |
| `metadata` | Map<string, string>   | no       | opaque to the port; passed through to adapters  |

Validation failures raise `INVALID_PAYLOAD` before any adapter is invoked. URL
schemes other than `https` (including `http`, `file`, `javascript`, `data`)
are rejected — see [Security invariants](#security-invariants).

---

## Send result

```
SendResult {
  ok: bool                              # true if at least one adapter succeeded
  results: list[AdapterOutcome]         # per-adapter, same order as adapters
  error_code: ErrorCode | null          # set when ok == false
  attempts: int                         # total attempts across all adapters
}

AdapterOutcome {
  adapter: string                       # adapter.name
  status: "DELIVERED" | "DELIVERED_SILENT" | "RETRIABLE_FAILURE" | "PERMANENT_FAILURE" | "BUSINESS_RATE_LIMITED"
  attempts: int
  error_code: ErrorCode | null          # set when status != DELIVERED*
  detail: string | null                 # human-readable, never includes secrets
}
```

`DELIVERED_SILENT` is reported when the adapter delivered the message in
silent mode due to active quiet hours.

---

## Quiet hours

Quiet hours **never drop** a message. Inside the quiet window, severities not
in `bypass_severities` are delivered in **silent mode**; the per-adapter
interpretation of "silent" is documented in adapter docs (Pushover priority -1,
Telegram `disable_notification: true`, Discord `flags: 4096`, SMTP no-op,
iMessage no-op, Slack no-op).

| Config key           | Type     | Default                      | Notes                                          |
|----------------------|----------|------------------------------|------------------------------------------------|
| `start`              | `HH:MM`  | required if section enabled  | local-time hour-minute                         |
| `end`                | `HH:MM`  | required if section enabled  | exclusive; wraps past midnight                 |
| `timezone`           | string   | required if section enabled  | IANA tz name (`Europe/Istanbul`)               |
| `bypass_severities`  | string[] | `["CRITICAL"]`               | severities that ignore quiet hours             |

When `quiet_hours` is `null`, all messages go through at the adapter's normal
priority for the given severity.

---

## Rate limiting

The package enforces **business-level** per-adapter rate limits, distinct from
upstream service rate limits returned in HTTP responses. Both are observable
in error codes:

- `BUSINESS_RATE_LIMITED` — the package's own configured policy blocked the
  send for this adapter. Caller may retry later or accept the suppression.
- `RATE_LIMITED` — the upstream service returned a 429 (or equivalent). This
  is a **retriable** adapter-level outcome.

The two MUST NOT be conflated. Upstream services may revise their published
rate limits at any time; package defaults below MUST be treated as guidance
and overridable.

| Adapter   | Default policy        | Source                                                  |
|-----------|-----------------------|---------------------------------------------------------|
| Pushover  | 5 / 60s               | Conservative burst guard; Pushover allows ≈10k/month overall |
| Telegram  | 1 / 1s per chat       | Telegram Bot API documented limit                       |
| Slack     | 1 / 1s per webhook    | Slack incoming-webhook documented guidance              |
| Discord   | 30 / 60s per webhook  | Discord webhook documented guidance                     |
| SMTP      | unlimited (null)      | Provider-specific; consumer SHOULD set explicitly       |
| iMessage  | 3 / 60s               | Conservative burst guard; no OS-level throttle exists   |

Severities listed in `rate_limits.bypass_severities` (default
`["CRITICAL"]`) are exempt.

---

## Retry / backoff

Every adapter call is wrapped in retry logic.

| Config key       | Type | Default  | Notes                                                |
|------------------|------|----------|------------------------------------------------------|
| `max_attempts`   | int  | `3`      | Including the first attempt                          |
| `base_delay_ms`  | int  | `500`    | First retry delay (before jitter)                    |
| `max_delay_ms`   | int  | `10_000` | Per-attempt delay ceiling                            |
| `jitter_ratio`   | float| `0.2`    | ±20% multiplicative jitter applied to each delay     |

Delay sequence: `delay(n) = min(max_delay_ms, base_delay_ms * 2^(n-1)) * (1 ± jitter)`.

Only `RETRIABLE_FAILURE` outcomes are retried. `PERMANENT_FAILURE` and
`BUSINESS_RATE_LIMITED` are terminal for that adapter.

| Outcome                    | Retried? |
|----------------------------|----------|
| Network timeout / refused  | yes      |
| HTTP 5xx                   | yes      |
| HTTP 429 (`RATE_LIMITED`)  | yes      |
| HTTP 4xx (auth, schema)    | no       |
| `BUSINESS_RATE_LIMITED`    | no       |
| `INVALID_PAYLOAD`          | no       |

---

## Adapter protocol

Adapters implement this language-agnostic shape:

```
Adapter {
  name: string                  # stable identifier, e.g. "pushover", "slack-webhook"
  is_available() -> bool        # cheap precheck (creds present, platform OK)
  deliver(severity, payload, silent: bool) -> AdapterDeliverResult
}

AdapterDeliverResult {
  status: "DELIVERED" | "RETRIABLE_FAILURE" | "PERMANENT_FAILURE"
  error_code: ErrorCode | null
  detail: string | null
}
```

When `silent` is true, the adapter MUST translate it into the channel's
nearest equivalent (Pushover priority -1, Telegram `disable_notification`,
Discord `SUPPRESS_NOTIFICATIONS`) or document a no-op explicitly.

`is_available()` is checked once per `send` call; an adapter that returns
`false` is skipped with `PERMANENT_FAILURE / ADAPTER_UNAVAILABLE`.

---

## Multicast semantics

- The client invokes every available adapter in parallel.
- An adapter's failure does not affect any other adapter.
- `SendResult.ok` is `true` if at least one adapter returned `DELIVERED` or
  `DELIVERED_SILENT`.
- If every adapter returned a permanent failure (or business rate limit), the
  client returns `ok=false` with `error_code = ALL_ADAPTERS_FAILED` and the
  per-adapter outcomes in `results`.
- Order of completion is not guaranteed; order in `results` matches the
  constructor order.

---

## Error taxonomy

Every error code is a stable string in `SCREAMING_SNAKE_CASE`. Codes are never
renamed within the same contract revision.

### Client-level

| Code                       | Description                                              | Returned at |
|----------------------------|----------------------------------------------------------|-------------|
| `INVALID_PAYLOAD`          | Payload validation failed                                | before adapter dispatch |
| `BUSINESS_RATE_LIMITED`    | Package's per-adapter policy blocked the call (per-adapter outcome) | per adapter |
| `ALL_ADAPTERS_FAILED`      | Every adapter returned a permanent failure or business rate limit | aggregate (`SendResult.error_code`) |

### Adapter-level

| Code                       | Description                                          | Class       |
|----------------------------|------------------------------------------------------|-------------|
| `ADAPTER_UNAVAILABLE`      | `is_available()` returned false (creds, platform)    | PERMANENT   |
| `RATE_LIMITED`             | Upstream returned HTTP 429 or equivalent             | RETRIABLE   |
| `AUTH_FAILED`              | Upstream returned 401 / 403                          | PERMANENT   |
| `ADAPTER_INVALID_PAYLOAD`  | Upstream rejected payload (HTTP 400)                 | PERMANENT   |
| `TIMEOUT`                  | Network or subprocess timeout                        | RETRIABLE   |
| `UPSTREAM_ERROR`           | Upstream HTTP 5xx                                    | RETRIABLE   |

`detail` strings MUST NOT include credentials, tokens, or full response bodies
that may contain user data — see [Security invariants](#security-invariants).

---

## Config schema

```
NotificationConfig {
  retry: RetryConfig | null              # default: max_attempts=3, base_delay_ms=500, max_delay_ms=10_000, jitter_ratio=0.2
  quiet_hours: QuietHoursConfig | null   # default: null (disabled)
  rate_limits: Map<string, RateLimitPolicy> | null  # key = adapter.name; default: per-adapter sane defaults documented above
}

RateLimitPolicy {
  max_count: int
  window_seconds: int
  bypass_severities: string[]            # default: ["CRITICAL"]
}
```

The package does **not** load configuration from any source. The consumer
constructs `NotificationConfig` from its own source (env, file,
`contriwork-config-core`, DB) and passes the typed object to
`NotificationClient`.

---

## Security invariants

These properties hold across all releases at this contract revision:

1. **Title and body length caps** (200 / 2000 chars) are enforced **before**
   any adapter is invoked. Inputs above the cap raise `INVALID_PAYLOAD`.
2. **URL scheme allow-list**: only `https://`. `http`, `file`, `javascript`,
   `data` URIs raise `INVALID_PAYLOAD`.
3. **No secret leakage in error detail**: per-adapter `detail` strings MUST be
   generic ("Notification delivery failed", "Authentication rejected", ...)
   — never echo tokens, response headers, or body fragments that may include
   sensitive data.
4. **AppleScript injection prevention** (Python-only iMessage adapter):
   `\\`, `"`, `` ` ``, `\r`, `\n`, `\t` MUST be escaped before composing the
   `osascript` command. Tracked as mitigation **PTV3-04** in `SECURITY.md`.
5. **Subprocess timeout**: iMessage adapter sets a hard 10-second timeout on
   `osascript` and reports `TIMEOUT` on expiry.
6. **No filesystem or network access from the orchestrator** — only adapters
   touch the network or subprocesses.

---

## Invariants

- Method signatures do not change shape (arg count, return type) without a
  contract bump.
- Severity enum order and names are frozen for the v1 revision.
- Error codes are never renamed within v1; they MAY be added.
- Config keys and their defaults are not changed within v1.
- Adapter protocol grows additively — method removal requires v2.

---

## Compatibility

- **Python**: ≥ 3.13 (see `VERSION_MATRIX.md`).
- **.NET**: ≥ 10.0 LTS.
- **Node.js**: ≥ 24 Active LTS.
- **npm strategy**: pure-TS reimplementation (Strategy A).

Single LTS per language by policy — no parallel matrix support for older
LTSes.

---

## Out of scope (consumer responsibility)

The following are intentionally **not** in this package and will not be added
without a documented second-consumer requirement:

- **Persistent storage**: in-app notification history, sent-message log, or DB
  writes of any kind.
- **Deduplication**: idempotency keys, content hashing, or suppression
  windows. Consumers needing dedup wrap `NotificationClient` themselves.
- **Channel routing**: mapping a semantic name (`"ops"`, `"user-alerts"`) to
  an ordered set of adapters. Consumers maintain their own
  `Map<channel, list[Adapter]>` and pass the resolved list to the client.
- **Inbound message reading** (e.g., reading replies from chat.db, parsing
  `/commands`). The iMessage adapter is **outbound only**.
- **Webhook receivers**, **delivery acknowledgement tracking**, **read
  receipts** — none.
- **Configuration loading**: env, files, DB, secret managers. Consumers load
  their own config and pass typed objects in.

---

## Change Log

Contract revisions only — bumped when any of the sections above change in a
way a consumer can observe. Patch fixes and internal refactors go in
`CHANGELOG.md`.

| Revision | Summary                                           | Released with package version |
|----------|---------------------------------------------------|-------------------------------|
| v1       | Initial public surface: `send` + 5-level severity + multicast adapter list + retry + business rate limit + quiet-hours silent downgrade + iMessage outbound (Python-only). | 0.1.0 |
