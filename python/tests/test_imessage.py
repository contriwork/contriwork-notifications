"""Per-adapter unit tests for IMessageAdapter (macOS-only adapter).

The adapter itself is platform-agnostic to import; runtime dispatch is
gated on platform.system(). Most tests monkeypatch ``platform.system``
and ``subprocess.run`` so they run on every CI host. The AppleScript
escape helper has its own tests and runs everywhere.
"""

from __future__ import annotations

import asyncio
import platform
import subprocess
from types import SimpleNamespace

import pytest

from contriwork_notifications import AdapterStatus, ErrorCode, Payload, Severity
from contriwork_notifications.macos import IMessageAdapter
from contriwork_notifications.macos.imessage import _applescript_escape


def test_escape_handles_backslash_first() -> None:
    assert _applescript_escape("a\\b") == "a\\\\b"


def test_escape_quotes_become_backslash_quote() -> None:
    assert _applescript_escape('he said "hi"') == 'he said \\"hi\\"'


def test_escape_drops_backticks() -> None:
    assert _applescript_escape("`whoami`") == "whoami"


def test_escape_normalises_whitespace() -> None:
    # \n -> " ", \t -> " ", \r is dropped (so "c\rd" collapses to "cd").
    assert _applescript_escape("a\nb\tc\rd") == "a b cd"


def test_escape_combined_payload() -> None:
    out = _applescript_escape('"; do shell script `whoami`\n')
    assert "\n" not in out
    assert "`" not in out
    assert '\\"' in out


async def test_off_macos_returns_adapter_unavailable(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(platform, "system", lambda: "Linux")
    adapter = IMessageAdapter(recipient="+15551234567")
    result = await adapter.deliver(Severity.INFO, Payload(title="x", body="y"), silent=False)
    assert result.status == AdapterStatus.PERMANENT_FAILURE
    assert result.error_code == ErrorCode.ADAPTER_UNAVAILABLE


async def test_is_available_off_macos(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(platform, "system", lambda: "Linux")
    assert not await IMessageAdapter(recipient="+1").is_available()


async def test_delivers_when_osascript_returns_zero(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(platform, "system", lambda: "Darwin")

    def fake_run(*_args: object, **_kwargs: object) -> SimpleNamespace:
        return SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(subprocess, "run", fake_run)
    result = await IMessageAdapter(recipient="+1").deliver(
        Severity.INFO, Payload(title="x", body="y"), silent=False
    )
    assert result.status == AdapterStatus.DELIVERED


async def test_permanent_failure_when_osascript_returns_nonzero(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(platform, "system", lambda: "Darwin")

    def fake_run(*_args: object, **_kwargs: object) -> SimpleNamespace:
        return SimpleNamespace(returncode=1, stdout="", stderr="permission denied")

    monkeypatch.setattr(subprocess, "run", fake_run)
    result = await IMessageAdapter(recipient="+1").deliver(
        Severity.INFO, Payload(title="x", body="y"), silent=False
    )
    assert result.status == AdapterStatus.PERMANENT_FAILURE
    assert result.error_code == ErrorCode.UPSTREAM_ERROR


async def test_timeout_when_osascript_times_out(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(platform, "system", lambda: "Darwin")

    def fake_run(*_args: object, **_kwargs: object) -> SimpleNamespace:
        raise subprocess.TimeoutExpired(cmd=["osascript"], timeout=10)

    monkeypatch.setattr(subprocess, "run", fake_run)
    result = await IMessageAdapter(recipient="+1").deliver(
        Severity.INFO, Payload(title="x", body="y"), silent=False
    )
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.TIMEOUT


def test_is_available_on_macos_with_recipient_only_when_running_on_macos() -> None:
    if platform.system() != "Darwin":
        return
    assert asyncio.run(IMessageAdapter(recipient="+1").is_available())
    assert not asyncio.run(IMessageAdapter(recipient="").is_available())
