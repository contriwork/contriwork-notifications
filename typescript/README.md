# @contriwork/notifications (npm)

Node.js / TypeScript adapter for the ContriWork **Notifications** port. One API surface, three languages (Python / .NET / npm) — this package is the Node.js implementation.

Cross-language specification, contract, and release history live in the
[GitHub repository](https://github.com/contriwork/contriwork-notifications):

- [Root README](https://github.com/contriwork/contriwork-notifications/blob/main/README.md) — ecosystem overview
- [`CONTRACT.md`](https://github.com/contriwork/contriwork-notifications/blob/main/CONTRACT.md) — language-agnostic port spec
- [`CHANGELOG.md`](https://github.com/contriwork/contriwork-notifications/blob/main/CHANGELOG.md)

Sister packages: [`contriwork-notifications`](https://pypi.org/project/contriwork-notifications/) (PyPI), [`Contriwork.Notifications`](https://www.nuget.org/packages/Contriwork.Notifications) (NuGet).

## Install

```bash
npm install @contriwork/notifications
# or: pnpm add @contriwork/notifications
# or: yarn add @contriwork/notifications
```

Requires **Node.js ≥ 24**. Dual-published ESM + CJS with bundled `.d.ts` / `.d.cts` type declarations. Published with [npm provenance](https://docs.npmjs.com/generating-provenance-statements) via GitHub Actions OIDC.

## Quick start

```ts
import {
  NotificationClient, Severity, SlackWebhookAdapter,
} from "@contriwork/notifications";

const client = new NotificationClient([
  new SlackWebhookAdapter({ webhookUrl: "https://hooks.slack.com/..." }),
]);

const result = await client.send(Severity.Warn, {
  title: "Build failed",
  body: "See CI for details",
});

console.log(result.ok, result.attempts);
```

For the full set of adapters (`InMemoryAdapter`, `PushoverAdapter`, `TelegramAdapter`, `SlackWebhookAdapter`, `DiscordWebhookAdapter`, `SmtpAdapter`) and the configuration knobs (`NotificationConfig`, `RetryConfig`, `RateLimitPolicy`, `QuietHoursConfig`), see [`CONTRACT.md`](https://github.com/contriwork/contriwork-notifications/blob/main/CONTRACT.md).

## Local development

```bash
pnpm install --frozen-lockfile
pnpm test
pnpm typecheck
pnpm lint
pnpm build
```

## License

MIT — see [LICENSE](https://github.com/contriwork/contriwork-notifications/blob/main/LICENSE).
