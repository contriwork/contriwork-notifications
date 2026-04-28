"""Telegram Bot API adapter — HTTPS POST to api.telegram.org/bot<token>/sendMessage."""

from __future__ import annotations

from typing import Any

import httpx

from ..adapter import AdapterDeliverResult, AdapterStatus
from ..errors import ErrorCode
from ..payload import Payload
from ..severity import Severity

_DEFAULT_API_BASE = "https://api.telegram.org"
_DEFAULT_TIMEOUT_S = 10.0

_GENERIC_FAILURE = "Notification delivery failed"
_GENERIC_AUTH = "Authentication rejected"


class TelegramAdapter:
    """Telegram Bot API delivery via HTTPS POST.

    The bot token is opaque to the package (consumer's responsibility to load
    from its own config source). Messages are sent as plain text to a single
    pre-configured chat id; multi-chat fan-out is the consumer's job.

    Silent mode (active quiet hours per the client config): forwarded as the
    Telegram Bot API ``disable_notification: true`` flag.
    """

    def __init__(
        self,
        bot_token: str,
        chat_id: str | int,
        *,
        name: str = "telegram",
        api_base: str = _DEFAULT_API_BASE,
        timeout_seconds: float = _DEFAULT_TIMEOUT_S,
    ) -> None:
        self._bot_token = bot_token
        self._chat_id = chat_id
        self.name = name
        self._api_base = api_base
        self._timeout = timeout_seconds

    async def is_available(self) -> bool:
        return bool(self._bot_token) and self._chat_id not in (None, "", 0)

    async def deliver(
        self,
        severity: Severity,
        payload: Payload,
        silent: bool,
    ) -> AdapterDeliverResult:
        del severity  # plain-text delivery; severity is not encoded in the wire payload
        url = f"{self._api_base}/bot{self._bot_token}/sendMessage"
        text = f"{payload.title}\n\n{payload.body}"
        if payload.url is not None:
            label = payload.url_title or payload.url
            text += f"\n\n{label}: {payload.url}"
        body: dict[str, Any] = {
            "chat_id": self._chat_id,
            "text": text,
            "disable_notification": silent,
        }

        try:
            async with httpx.AsyncClient(timeout=self._timeout) as client:
                response = await client.post(url, json=body)
        except httpx.TimeoutException:
            return AdapterDeliverResult(
                status=AdapterStatus.RETRIABLE_FAILURE,
                error_code=ErrorCode.TIMEOUT,
                detail=_GENERIC_FAILURE,
            )
        except httpx.HTTPError:
            return AdapterDeliverResult(
                status=AdapterStatus.RETRIABLE_FAILURE,
                error_code=ErrorCode.UPSTREAM_ERROR,
                detail=_GENERIC_FAILURE,
            )

        return _map_response(response.status_code)


def _map_response(status_code: int) -> AdapterDeliverResult:
    if 200 <= status_code < 300:
        return AdapterDeliverResult(status=AdapterStatus.DELIVERED)
    if status_code == 429:
        return AdapterDeliverResult(
            status=AdapterStatus.RETRIABLE_FAILURE,
            error_code=ErrorCode.RATE_LIMITED,
            detail=_GENERIC_FAILURE,
        )
    if status_code in (401, 403):
        return AdapterDeliverResult(
            status=AdapterStatus.PERMANENT_FAILURE,
            error_code=ErrorCode.AUTH_FAILED,
            detail=_GENERIC_AUTH,
        )
    if 400 <= status_code < 500:
        return AdapterDeliverResult(
            status=AdapterStatus.PERMANENT_FAILURE,
            error_code=ErrorCode.ADAPTER_INVALID_PAYLOAD,
            detail=_GENERIC_FAILURE,
        )
    return AdapterDeliverResult(
        status=AdapterStatus.RETRIABLE_FAILURE,
        error_code=ErrorCode.UPSTREAM_ERROR,
        detail=_GENERIC_FAILURE,
    )
