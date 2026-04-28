"""Pushover adapter — HTTPS POST to api.pushover.net."""

from __future__ import annotations

import httpx

from ..adapter import AdapterDeliverResult, AdapterStatus
from ..errors import ErrorCode
from ..payload import Payload
from ..severity import Severity

_DEFAULT_API_URL = "https://api.pushover.net/1/messages.json"
_DEFAULT_TIMEOUT_S = 10.0

# CONTRACT.md §Security invariants: detail strings never echo tokens or
# response bodies. Generic strings only.
_GENERIC_FAILURE = "Notification delivery failed"
_GENERIC_AUTH = "Authentication rejected"


class PushoverAdapter:
    """Pushover delivery via HTTPS POST.

    Severity → Pushover priority mapping:
      * DEBUG    → -1 (silent / badge-only)
      * INFO     →  0
      * WARN     →  0
      * ERROR    →  ``error_priority`` (default 0; pass 1 to escalate)
      * CRITICAL →  1 (high priority; bypasses Pushover's own quiet hours)

    Silent mode (active quiet hours per the client config): priority is
    forced to -1. Severities listed in the client's ``bypass_severities``
    never reach silent mode in the first place.
    """

    def __init__(
        self,
        user_key: str,
        app_token: str,
        *,
        name: str = "pushover",
        error_priority: int = 0,
        api_url: str = _DEFAULT_API_URL,
        timeout_seconds: float = _DEFAULT_TIMEOUT_S,
    ) -> None:
        if error_priority not in (0, 1):
            raise ValueError("error_priority must be 0 or 1")
        self._user_key = user_key
        self._app_token = app_token
        self.name = name
        self._error_priority = error_priority
        self._api_url = api_url
        self._timeout = timeout_seconds

    async def is_available(self) -> bool:
        return bool(self._user_key and self._app_token)

    async def deliver(
        self,
        severity: Severity,
        payload: Payload,
        silent: bool,
    ) -> AdapterDeliverResult:
        priority = -1 if silent else self._priority_for(severity)
        data: dict[str, str | int] = {
            "token": self._app_token,
            "user": self._user_key,
            "title": payload.title,
            "message": payload.body,
            "priority": priority,
        }
        if payload.url is not None:
            data["url"] = payload.url
        if payload.url_title is not None:
            data["url_title"] = payload.url_title

        try:
            async with httpx.AsyncClient(timeout=self._timeout) as client:
                response = await client.post(self._api_url, data=data)
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

    def _priority_for(self, severity: Severity) -> int:
        if severity == Severity.DEBUG:
            return -1
        if severity == Severity.CRITICAL:
            return 1
        if severity == Severity.ERROR:
            return self._error_priority
        return 0  # INFO, WARN


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
