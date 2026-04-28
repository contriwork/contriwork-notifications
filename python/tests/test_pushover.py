"""Per-adapter unit tests for PushoverAdapter."""

from __future__ import annotations

import httpx
import pytest
from pytest_httpx import HTTPXMock

from contriwork_notifications import AdapterStatus, ErrorCode, Payload, Severity
from contriwork_notifications.adapters import PushoverAdapter

_API = "https://api.pushover.net/1/messages.json"
_PAYLOAD = Payload(title="hi", body="world")


async def test_delivers_on_2xx(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_API, status_code=200, json={"status": 1})
    adapter = PushoverAdapter(user_key="u", app_token="t")
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.DELIVERED
    assert result.error_code is None


async def test_auth_failed_on_401(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_API, status_code=401)
    adapter = PushoverAdapter(user_key="u", app_token="t")
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.PERMANENT_FAILURE
    assert result.error_code == ErrorCode.AUTH_FAILED


async def test_invalid_payload_on_400(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_API, status_code=400)
    adapter = PushoverAdapter(user_key="u", app_token="t")
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.PERMANENT_FAILURE
    assert result.error_code == ErrorCode.ADAPTER_INVALID_PAYLOAD


async def test_rate_limited_on_429(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_API, status_code=429)
    adapter = PushoverAdapter(user_key="u", app_token="t")
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.RATE_LIMITED


async def test_upstream_error_on_500(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_API, status_code=502)
    adapter = PushoverAdapter(user_key="u", app_token="t")
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.UPSTREAM_ERROR


async def test_timeout_on_network_timeout(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_exception(httpx.ConnectTimeout("simulated"))
    adapter = PushoverAdapter(user_key="u", app_token="t")
    result = await adapter.deliver(Severity.INFO, _PAYLOAD, silent=False)
    assert result.status == AdapterStatus.RETRIABLE_FAILURE
    assert result.error_code == ErrorCode.TIMEOUT


async def test_silent_forces_priority_minus_one(httpx_mock: HTTPXMock) -> None:
    httpx_mock.add_response(method="POST", url=_API, status_code=200)
    adapter = PushoverAdapter(user_key="u", app_token="t")
    await adapter.deliver(Severity.WARN, _PAYLOAD, silent=True)
    request = httpx_mock.get_request()
    assert request is not None
    body = request.read().decode()
    assert "priority=-1" in body


def test_is_available_requires_credentials() -> None:
    import asyncio

    assert asyncio.run(PushoverAdapter(user_key="u", app_token="t").is_available())
    assert not asyncio.run(PushoverAdapter(user_key="", app_token="t").is_available())
    assert not asyncio.run(PushoverAdapter(user_key="u", app_token="").is_available())


def test_constructor_rejects_invalid_error_priority() -> None:
    with pytest.raises(ValueError, match="error_priority"):
        PushoverAdapter(user_key="u", app_token="t", error_priority=2)
