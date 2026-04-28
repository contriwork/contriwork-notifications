"""Smoke tests for the public surface."""

from __future__ import annotations

import asyncio

import contriwork_notifications
from contriwork_notifications import (
    NotificationClient,
    NotificationConfig,
    NotificationPort,
    OutcomeStatus,
    Payload,
    Severity,
)

_PUBLIC_SYMBOLS = {
    "Adapter",
    "AdapterDeliverResult",
    "AdapterOutcome",
    "AdapterStatus",
    "ErrorCode",
    "InMemoryAdapter",
    "NotificationClient",
    "PushoverAdapter",
    "NotificationConfig",
    "NotificationPort",
    "OutcomeStatus",
    "Payload",
    "QuietHoursConfig",
    "RateLimitPolicy",
    "RetryConfig",
    "SendResult",
    "Severity",
    "SlackWebhookAdapter",
    "TelegramAdapter",
}


def test_version_is_set() -> None:
    assert contriwork_notifications.__version__


def test_public_surface_is_complete() -> None:
    missing = sorted(s for s in _PUBLIC_SYMBOLS if not hasattr(contriwork_notifications, s))
    assert missing == [], f"missing public symbols: {missing}"


def test_severity_icons_are_stable() -> None:
    assert Severity.DEBUG.icon == "🔍"
    assert Severity.INFO.icon == "ℹ️"
    assert Severity.WARN.icon == "⚠️"
    assert Severity.ERROR.icon == "❌"
    assert Severity.CRITICAL.icon == "⛔"


def test_empty_adapter_list_is_a_noop() -> None:
    client = NotificationClient(adapters=[], config=NotificationConfig())
    result = asyncio.run(client.send(Severity.INFO, Payload(title="hi", body="world")))
    assert result.ok is True
    assert result.results == []
    assert result.error_code is None
    assert result.attempts == 0


def test_invalid_payload_short_circuits() -> None:
    client = NotificationClient(adapters=[], config=NotificationConfig())
    # title over 200 chars
    payload = Payload(title="A" * 201, body="ok")
    result = asyncio.run(client.send(Severity.INFO, payload))
    assert result.ok is False
    assert result.error_code == "INVALID_PAYLOAD"
    assert result.attempts == 0


def test_invalid_payload_non_https_url_short_circuits() -> None:
    client = NotificationClient(adapters=[], config=NotificationConfig())
    payload = Payload(title="hi", body="ok", url="http://example.com")
    result = asyncio.run(client.send(Severity.INFO, payload))
    assert result.ok is False
    assert result.error_code == "INVALID_PAYLOAD"


def test_client_satisfies_notification_port_protocol() -> None:
    client = NotificationClient(adapters=[])
    assert isinstance(client, NotificationPort)


def test_outcome_status_values_are_stable() -> None:
    assert OutcomeStatus.DELIVERED == "DELIVERED"
    assert OutcomeStatus.DELIVERED_SILENT == "DELIVERED_SILENT"
    assert OutcomeStatus.RETRIABLE_FAILURE == "RETRIABLE_FAILURE"
    assert OutcomeStatus.PERMANENT_FAILURE == "PERMANENT_FAILURE"
    assert OutcomeStatus.BUSINESS_RATE_LIMITED == "BUSINESS_RATE_LIMITED"
