# SCOPE — Notifications package

This document records what is and is not included in `contriwork-notifications`,
the rationale for those choices, and the deferred items that may be added in
later releases.

The authoritative public surface is in [`CONTRACT.md`](../CONTRACT.md). This
file is the **history and rationale** behind that surface — it complements the
contract, it does not replace it.

---

## v0.1.0 adapter scope

| Adapter                  | Python      | C# | TypeScript | Notes                                                                                              |
|--------------------------|:-----------:|:--:|:----------:|----------------------------------------------------------------------------------------------------|
| `InMemoryAdapter`        | ✅          | ✅ | ✅         | Required for contract-test fixtures; not a production adapter.                                     |
| `PushoverAdapter`        | ✅          | ✅ | ✅         | HTTPS POST with API token + user key.                                                              |
| `TelegramAdapter`        | ✅          | ✅ | ✅         | Bot API `/sendMessage` over HTTPS.                                                                 |
| `SlackWebhookAdapter`    | ✅          | ✅ | ✅         | Incoming webhook URL with JSON payload.                                                            |
| `DiscordWebhookAdapter`  | ✅          | ✅ | ✅         | Webhook URL with JSON payload (different schema from Slack).                                       |
| `SmtpAdapter`            | ✅          | ✅ | ✅         | `aiosmtplib` / `MailKit` / `nodemailer`.                                                           |
| `IMessageAdapter`        | ✅ (opt-in) | —  | —          | macOS-only `osascript`. Imported via `contriwork_notifications.macos`. Not provided in C# or TypeScript. |

### Why these adapters in v0.1.0

- **Pushover, Telegram, Slack, Discord** — all four are simple HTTPS POSTs with
  low credential overhead and broad real-world adoption; including them out of
  the gate raises the package's potential reach without increasing delivery
  surface complexity.
- **SMTP** — covers the email case without taking on a provider-specific API
  surface.
- **iMessage** — ships **only on Python** because the implementation depends
  on macOS `osascript`. C# and TypeScript would each require platform-specific
  interop with no parity benefit; if a real demand appears, parity is
  reconsidered, not pre-emptively built.
- **InMemoryAdapter** — exists purely so contract tests can drive every
  orchestrator behavior without touching network or subprocess.

---

## Deferred to a later release

These are explicitly **not** in v0.1.0. They will be reconsidered when a
documented requirement appears (an issue describing the use case) — not
before, per the project's no-over-engineering rule.

### Adapters

- **Twilio SMS** — Account + cost setup overhead; no documented requirement.
- **APNs / FCM** — Mobile push transports; no in-scope mobile use case.
- **Apprise** — Meta-adapter wrapping ~80 services. Useful but redundant with
  the per-service adapters we ship; only justified once a long-tail demand
  appears.
- **Provider-specific email (SES / SendGrid / Resend)** — `SmtpAdapter`
  already covers the email case for v0.1.0.

### Features

- **Channel routing abstraction** — Mapping a semantic name (`"ops"`,
  `"user-alerts"`) to an ordered set of adapters. The package today takes a
  flat adapter list; consumers maintain their own
  `Map<channel, list[Adapter]>`.
- **Deduplication** — Idempotency keys, content hashing, suppression windows.
  Consumers needing dedup wrap `NotificationClient` themselves. There is no
  in-package dedup store (in-memory or persistent).
- **Inbound message reading / command parsing** — Reading replies from chat
  databases or webhook receivers, parsing user commands. The iMessage adapter
  is **outbound only**.
- **Persistent state** — Notification history, sent-message log, DB writes of
  any kind. The package does not own a database.
- **Configuration loading** — Env, files, secret managers, dynamic reload.
  The package only exposes a typed config schema; consumers load their own
  values and pass typed objects in.

---

## How to expand scope

A scope addition requires:

1. A documented requirement (an issue describing the use case) — not
   speculative.
2. A `CONTRACT.md` revision proposal if the public surface changes.
3. An updated entry in this file recording the rationale.
4. A `VERSION_MATRIX.md` row for the release that introduces the change.

Pure additions to the adapter set (no contract change) only need steps 1, 3,
and 4.
