"""Per-adapter unit tests for SmtpAdapter (aiosmtplib monkeypatched)."""

from __future__ import annotations

import asyncio

import aiosmtplib
import pytest

from contriwork_notifications import AdapterStatus, ErrorCode, Payload, Severity
from contriwork_notifications.adapters import SmtpAdapter

_PAYLOAD = Payload(title="hi", body="world")


def _make_adapter() -> SmtpAdapter:
    return SmtpAdapter(
        host="smtp.example.com",
        port=587,
        from_addr="alerts@example.com",
        to_addrs=["dest@example.com"],
        username="alerts",
        password="secret",
    )


async def test_delivers_on_send_success(monkeypatch: pytest.MonkeyPatch) -> None:
    captured: dict[str, object] = {}

    async def fake_send(msg: object, **kwargs: object) -> None:
        captured["msg"] = msg
        captured["kwargs"] = kwargs

    monkeypatch.setattr(aiosmtplib, "send", fake_send)
    result = await _make_adapter().deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.DELIVERED
    assert captured["msg"] is not None


async def test_auth_failed(monkeypatch: pytest.MonkeyPatch) -> None:
    async def fake_send(*_args: object, **_kwargs: object) -> None:
        raise aiosmtplib.SMTPAuthenticationError(535, "bad creds")

    monkeypatch.setattr(aiosmtplib, "send", fake_send)
    result = await _make_adapter().deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.PERMANENT_FAILURE
    assert result.error_code == ErrorCode.AUTH_FAILED


async def test_recipients_refused(monkeypatch: pytest.MonkeyPatch) -> None:
    async def fake_send(*_args: object, **_kwargs: object) -> None:
        raise aiosmtplib.SMTPRecipientsRefused([])

    monkeypatch.setattr(aiosmtplib, "send", fake_send)
    result = await _make_adapter().deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.PERMANENT_FAILURE
    assert result.error_code == ErrorCode.ADAPTER_INVALID_PAYLOAD


async def test_timeout(monkeypatch: pytest.MonkeyPatch) -> None:
    async def fake_send(*_args: object, **_kwargs: object) -> None:
        raise TimeoutError("simulated")

    monkeypatch.setattr(aiosmtplib, "send", fake_send)
    result = await _make_adapter().deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.TIMEOUT


async def test_upstream_error_on_smtp_exception(monkeypatch: pytest.MonkeyPatch) -> None:
    async def fake_send(*_args: object, **_kwargs: object) -> None:
        raise aiosmtplib.SMTPException("boom")

    monkeypatch.setattr(aiosmtplib, "send", fake_send)
    result = await _make_adapter().deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.UPSTREAM_ERROR


def test_is_available_requires_required_fields() -> None:
    assert asyncio.run(_make_adapter().is_available())
    assert not asyncio.run(
        SmtpAdapter(
            host="",
            port=587,
            from_addr="a@b.c",
            to_addrs=["d@e.f"],
        ).is_available()
    )
    assert not asyncio.run(
        SmtpAdapter(
            host="smtp.example.com",
            port=587,
            from_addr="a@b.c",
            to_addrs=[],
        ).is_available()
    )
