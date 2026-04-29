<div align="center">

# contriwork-notifications

**One API surface, three languages.**

[![PyPI](https://img.shields.io/pypi/v/contriwork-notifications.svg)](https://pypi.org/project/contriwork-notifications/)
[![NuGet](https://img.shields.io/nuget/v/Contriwork.Notifications.svg)](https://www.nuget.org/packages/Contriwork.Notifications/)
[![npm](https://img.shields.io/npm/v/@contriwork/notifications.svg)](https://www.npmjs.com/package/@contriwork/notifications)
[![CI](https://github.com/contriwork/contriwork-notifications/actions/workflows/ci.yml/badge.svg)](https://github.com/contriwork/contriwork-notifications/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)

</div>

A transport-only notification library: send a `(severity, payload)` to one or more adapters (Slack, Discord, Telegram, Pushover, SMTP, in-memory) with built-in retry, quiet hours, and per-adapter rate limiting — same API surface in Python, .NET, and Node.js.

## Why

Most ContriWork consumers eventually need to push alerts to several places at once (chat webhook + email, sometimes a phone push). Doing this once per repo means re-implementing retry, quiet hours, and per-adapter rate limiting from scratch — and ending up with three subtly different implementations across Python / .NET / npm. This package centralises the transport layer behind a single contract; anything beyond `send` (deduplication, persistent queues, named channels, templating) stays in consumer code by design.

## Install

| Registry | Command                                                |
|----------|--------------------------------------------------------|
| PyPI     | `pip install contriwork-notifications`                  |
| NuGet    | `dotnet add package Contriwork.Notifications`            |
| npm      | `npm install @contriwork/notifications`                 |

All three publish at the **same version** on the **same release**. See [`VERSION_MATRIX.md`](./VERSION_MATRIX.md) for runtime support per release.

## Quick start

### Python

```python
import asyncio
from contriwork_notifications import (
    NotificationClient, Severity, Payload, SlackWebhookAdapter,
)

client = NotificationClient([SlackWebhookAdapter(webhook_url="https://hooks.slack.com/...")])
result = asyncio.run(client.send(Severity.WARN, Payload(title="Build failed", body="See CI for details")))
print(result.ok, result.attempts)
```

### C#

```csharp
using Contriwork.Notifications;
using Contriwork.Notifications.Adapters;

var client = new NotificationClient(new IAdapter[]
{
    new SlackWebhookAdapter("https://hooks.slack.com/..."),
});

var result = await client.SendAsync(Severity.Warn, new Payload("Build failed", "See CI for details"));
Console.WriteLine($"{result.Ok} {result.Attempts}");
```

### TypeScript

```typescript
import {
  NotificationClient, Severity, SlackWebhookAdapter,
} from "@contriwork/notifications";

const client = new NotificationClient([
  new SlackWebhookAdapter({ webhookUrl: "https://hooks.slack.com/..." }),
]);

const result = await client.send(Severity.Warn, { title: "Build failed", body: "See CI for details" });
console.log(result.ok, result.attempts);
```

## Architecture

This package follows the [ContriWork port + adapter](https://github.com/contriwork/.github) pattern:

- **Port** — language-agnostic interface defined in [`CONTRACT.md`](./CONTRACT.md).
- **Adapters** — concrete implementations per language (`python/src/`, `csharp/src/`, `typescript/src/`).
- **Contract tests** — shared fixture set in [`contract-tests/test_cases.json`](./contract-tests/test_cases.json), executed by all three language test runners. Release is blocked unless all three are green.

## Runtime baseline

| Language | Target               |
|----------|----------------------|
| Python   | **3.13**             |
| .NET     | **10 (LTS)**         |
| Node.js  | **24 (Active LTS)**  |

Single LTS per language by policy — no parallel matrix support for short-lived LTS releases.

## Security

See [`SECURITY.md`](./SECURITY.md) for the disclosure channel and the package's hardening posture (black-box / gray-box / white-box pen-test results, scan tooling, and remediation history).

## Contributing

Forks welcome. See [`CONTRIBUTING.md`](./CONTRIBUTING.md) for the DCO sign-off rule, the contract-test workflow, and branch protection details.

## License

MIT — see [`LICENSE`](./LICENSE).
