# Contriwork.Notifications (.NET)

.NET adapter for the ContriWork **Notifications** port. One API surface, three languages (Python / .NET / npm) — this package is the .NET implementation.

Cross-language specification, contract, and release history live in the
[GitHub repository](https://github.com/contriwork/contriwork-notifications):

- [Root README](https://github.com/contriwork/contriwork-notifications/blob/main/README.md) — ecosystem overview
- [`CONTRACT.md`](https://github.com/contriwork/contriwork-notifications/blob/main/CONTRACT.md) — language-agnostic port spec
- [`CHANGELOG.md`](https://github.com/contriwork/contriwork-notifications/blob/main/CHANGELOG.md)

Sister packages: [`contriwork-notifications`](https://pypi.org/project/contriwork-notifications/) (PyPI), [`@contriwork/notifications`](https://www.npmjs.com/package/@contriwork/notifications) (npm).

## Install

```bash
dotnet add package Contriwork.Notifications
```

Targets **.NET 10 LTS**.

## Quick start

```csharp
using Contriwork.Notifications;
using Contriwork.Notifications.Adapters;

var client = new NotificationClient(new IAdapter[]
{
    new SlackWebhookAdapter("https://hooks.slack.com/..."),
});

var result = await client.SendAsync(
    Severity.Warn,
    new Payload("Build failed", "See CI for details"));

Console.WriteLine($"{result.Ok} {result.Attempts}");
```

For the full set of adapters (`InMemoryAdapter`, `PushoverAdapter`, `TelegramAdapter`, `SlackWebhookAdapter`, `DiscordWebhookAdapter`, `SmtpAdapter`) and the configuration knobs (`NotificationConfig`, `RetryConfig`, `RateLimitPolicy`, `QuietHoursConfig`), see [`CONTRACT.md`](https://github.com/contriwork/contriwork-notifications/blob/main/CONTRACT.md).

## Local development

```bash
dotnet restore
dotnet build
dotnet test
dotnet format --verify-no-changes
```

## License

MIT — see [LICENSE](https://github.com/contriwork/contriwork-notifications/blob/main/LICENSE).
