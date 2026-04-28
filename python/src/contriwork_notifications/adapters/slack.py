"""Slack incoming-webhook adapter — HTTPS POST with a JSON body."""

from __future__ import annotations

from typing import Any

import httpx

from ..adapter import AdapterDeliverResult, AdapterStatus
from ..errors import ErrorCode
from ..payload import Payload
from ..severity import Severity

_DEFAULT_TIMEOUT_S = 10.0

_GENERIC_FAILURE = "Notification delivery failed"
_GENERIC_AUTH = "Authentication rejected"


class SlackWebhookAdapter:
    """Slack incoming-webhook delivery via HTTPS POST.

    The webhook URL is opaque to the package (consumer's responsibility to
    load from its own config source). The body is sent as plain text using
    Slack's mrkdwn-friendly conventions (``*bold title*`` + body); the
    package does not produce Block Kit payloads in v0.1.0.

    Slack incoming webhooks have no native silent flag. ``silent`` is a
    documented no-op; CONTRACT.md §Quiet hours flags this explicitly.
    """

    def __init__(
        self,
        webhook_url: str,
        *,
        name: str = "slack-webhook",
        timeout_seconds: float = _DEFAULT_TIMEOUT_S,
    ) -> None:
        self._webhook_url = webhook_url
        self.name = name
        self._timeout = timeout_seconds

    async def is_available(self) -> bool:
        return bool(self._webhook_url)

    async def deliver(
        self,
        severity: Severity,
        payload: Payload,
        silent: bool,
    ) -> AdapterDeliverResult:
        del severity, silent  # see class docstring; silent is a no-op
        text = f"*{payload.title}*\n{payload.body}"
        if payload.url is not None:
            label = payload.url_title or payload.url
            text += f"\n<{payload.url}|{label}>"
        body: dict[str, Any] = {"text": text}

        try:
            async with httpx.AsyncClient(timeout=self._timeout) as client:
                response = await client.post(self._webhook_url, json=body)
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
