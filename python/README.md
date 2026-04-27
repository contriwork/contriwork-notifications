# contriwork-notifications (Python)

Python adapter for the ContriWork **notifications** port. One API surface, three languages (Python / .NET / npm) — this package is the Python implementation.

Cross-language specification, contract, and release history live in the
[GitHub repository](https://github.com/contriwork/contriwork-notifications):

- [Root README](https://github.com/contriwork/contriwork-notifications/blob/main/README.md) — ecosystem overview
- [`CONTRACT.md`](https://github.com/contriwork/contriwork-notifications/blob/main/CONTRACT.md) — language-agnostic port spec
- [`CHANGELOG.md`](https://github.com/contriwork/contriwork-notifications/blob/main/CHANGELOG.md)

Sister packages: [`Contriwork.Notifications`](https://www.nuget.org/packages/Contriwork.Notifications) (NuGet), [`@contriwork/notifications`](https://www.npmjs.com/package/@contriwork/notifications) (npm).

## Install

```bash
pip install contriwork-notifications
```

Requires **Python ≥ 3.13**.

## Quick start

```python
from contriwork_notifications import NotificationsPort

# TODO: one-line example once the port has real methods.
```

## Local development

```bash
uv sync --all-extras
uv run pytest
uv run ruff check
uv run mypy src
```

## License

MIT — see [LICENSE](https://github.com/contriwork/contriwork-notifications/blob/main/LICENSE).
