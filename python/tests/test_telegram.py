"""Per-adapter unit tests for TelegramAdapter."""

from __future__ import annotations

import json

import httpx
from pytest_httpx import HTTPXMock

from contriwork_notifications import AdapterStatus, ErrorCode, Payload, Severity
from contriwork_notifications.adapters import TelegramAdapter

_API = "https://api.telegram.org/botBOT_TOKEN/sendMessage"
_PAYLOAD = Payload(title="hi", body="world")


async def test_delivers_on_2xx(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_API, status_code=200, json={"ok": True})
    adapter = TelegramAdapter(bot_token="BOT_TOKEN", chat_id=123)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.DELIVERED


async def test_auth_failed_on_401(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_API, status_code=401)
    adapter = TelegramAdapter(bot_token="BOT_TOKEN", chat_id=123)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.PERMANENT_FAILURE
    assert result.error_code == ErrorCode.AUTH_FAILED


async def test_rate_limited_on_429(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_API, status_code=429)
    adapter = TelegramAdapter(bot_token="BOT_TOKEN", chat_id=123)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.RATE_LIMITED


async def test_upstream_error_on_500(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_API, status_code=503)
    adapter = TelegramAdapter(bot_token="BOT_TOKEN", chat_id=123)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.UPSTREAM_ERROR


async def test_timeout_on_network_timeout(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_exception(httpx.ReadTimeout("simulated"))
    adapter = TelegramAdapter(bot_token="BOT_TOKEN", chat_id=123)
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.TIMEOUT


async def test_silent_sets_disable_notification(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_API, status_code=200, json={"ok": True})
    adapter = TelegramAdapter(bot_token="BOT_TOKEN", chat_id=123)
    await adapter.deliver(Severity.WARN, _PAYLOAD, silent=True)
    request = httpx_mock.get_request()
    assert request is not None
    body = json.loads(request.read())
    assert body["disable_notification"] is True


async def test_url_appended_to_text(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_API, status_code=200, json={"ok": True})
    adapter = TelegramAdapter(bot_token="BOT_TOKEN", chat_id=123)
    payload = Payload(title="t", body="b", url="https://example.com", url_title="link")
    await adapter.deliver(Severity.INFO, payload, silent=False)
    body = json.loads(httpx_mock.get_request().read())  # type: ignore[union-attr]
    text: str = body["text"]
    # endswith is anchored, so a trailing "https://example.com.evil" would not match.
    assert text.endswith("link: https://example.com")
