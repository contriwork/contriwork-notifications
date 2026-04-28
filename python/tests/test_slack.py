"""Per-adapter unit tests for SlackWebhookAdapter."""

from __future__ import annotations

import json

import httpx
from pytest_httpx import HTTPXMock

from contriwork_notifications import AdapterStatus, ErrorCode, Payload, Severity
from contriwork_notifications.adapters import SlackWebhookAdapter

_WEBHOOK = "https://hooks.slack.com/services/T0/B0/secret"
_PAYLOAD = Payload(title="hi", body="world")


async def test_delivers_on_2xx(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_WEBHOOK, status_code=200, text="ok")
    adapter = SlackWebhookAdapter(webhook_url=_WEBHOOK)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.DELIVERED


async def test_invalid_payload_on_400(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_WEBHOOK, status_code=400)
    adapter = SlackWebhookAdapter(webhook_url=_WEBHOOK)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.PERMANENT_FAILURE
    assert result.error_code == ErrorCode.ADAPTER_INVALID_PAYLOAD


async def test_rate_limited_on_429(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_WEBHOOK, status_code=429)
    adapter = SlackWebhookAdapter(webhook_url=_WEBHOOK)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.RATE_LIMITED


async def test_upstream_error_on_500(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_WEBHOOK, status_code=500)
    adapter = SlackWebhookAdapter(webhook_url=_WEBHOOK)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.UPSTREAM_ERROR


async def test_timeout(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_exception(httpx.ReadTimeout("simulated"))
    adapter = SlackWebhookAdapter(webhook_url=_WEBHOOK)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.TIMEOUT


async def test_silent_is_no_op_in_payload(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_WEBHOOK, status_code=200, text="ok")
    adapter = SlackWebhookAdapter(webhook_url=_WEBHOOK)
    await adapter.deliver(Severity.INFO, _PAYLOAD, silent=True)
    request = httpx_mock.get_request()
    assert request is not None
    body = json.loads(request.read())
    # No native silent flag exists; payload is identical regardless of silent.
    assert "flags" not in body
    assert "disable_notification" not in body
