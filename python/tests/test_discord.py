"""Per-adapter unit tests for DiscordWebhookAdapter."""

from __future__ import annotations

import json

import httpx
from pytest_httpx import HTTPXMock

from contriwork_notifications import AdapterStatus, ErrorCode, Payload, Severity
from contriwork_notifications.adapters import DiscordWebhookAdapter

_WEBHOOK = "https://discord.com/api/webhooks/123/secret"
_PAYLOAD = Payload(title="hi", body="world")


async def test_delivers_on_204(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_WEBHOOK, status_code=204)
    adapter = DiscordWebhookAdapter(webhook_url=_WEBHOOK)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.DELIVERED


async def test_invalid_payload_on_400(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_WEBHOOK, status_code=400)
    adapter = DiscordWebhookAdapter(webhook_url=_WEBHOOK)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.PERMANENT_FAILURE
    assert result.error_code == ErrorCode.ADAPTER_INVALID_PAYLOAD


async def test_rate_limited_on_429(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_WEBHOOK, status_code=429)
    adapter = DiscordWebhookAdapter(webhook_url=_WEBHOOK)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.RATE_LIMITED


async def test_upstream_error_on_500(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_WEBHOOK, status_code=500)
    adapter = DiscordWebhookAdapter(webhook_url=_WEBHOOK)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.UPSTREAM_ERROR


async def test_timeout(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_exception(httpx.ConnectTimeout("simulated"))
    adapter = DiscordWebhookAdapter(webhook_url=_WEBHOOK)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.TIMEOUT


async def test_silent_sets_suppress_notifications_flag(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_WEBHOOK, status_code=204)
    adapter = DiscordWebhookAdapter(webhook_url=_WEBHOOK)
    await adapter.deliver(Severity.INFO, _PAYLOAD, silent=True)
    request = httpx_mock.get_request()
    assert request is not None
    body = json.loads(request.read())
    assert body["flags"] == 4096


async def test_non_silent_omits_flags(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_WEBHOOK, status_code=204)
    adapter = DiscordWebhookAdapter(webhook_url=_WEBHOOK)
    await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    request = httpx_mock.get_request()
    assert request is not None
    body = json.loads(request.read())
    assert "flags" not in body
