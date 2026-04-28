"""SMTP adapter — outbound email via aiosmtplib."""

from __future__ import annotations

from collections.abc import Sequence
from email.message import EmailMessage

import aiosmtplib

from ..adapter import AdapterDeliverResult, AdapterStatus
from ..errors import ErrorCode
from ..payload import Payload
from ..severity import Severity

_DEFAULT_TIMEOUT_S = 30.0

_GENERIC_FAILURE = "Notification delivery failed"
_GENERIC_AUTH = "Authentication rejected"


class SmtpAdapter:
    """SMTP delivery via aiosmtplib.

    Default transport is STARTTLS on port 587. For implicit TLS (port 465)
    pass ``use_tls=True`` and ``start_tls=False``. Both connect parameters
    and credentials are opaque to the package; the consumer loads them
    from its own config source.

    Email has no programmatic silent flag, so ``silent`` is a no-op
    (CONTRACT.md §Quiet hours flags this explicitly). The orchestrator
    still reports DELIVERED_SILENT for accounting; the wire payload is
    identical.
    """

    def __init__(
        self,
        host: str,
        port: int,
        from_addr: str,
        to_addrs: Sequence[str],
        *,
        username: str | None = None,
        password: str | None = None,
        use_tls: bool = False,
        start_tls: bool = True,
        name: str = "smtp",
        timeout_seconds: float = _DEFAULT_TIMEOUT_S,
    ) -> None:
        self._host = host
        self._port = port
        self._from_addr = from_addr
        self._to_addrs = list(to_addrs)
        self._username = username
        self._password = password
        self._use_tls = use_tls
        self._start_tls = start_tls
        self.name = name
        self._timeout = timeout_seconds

    async def is_available(self) -> bool:
        return bool(self._host and self._port and self._from_addr and self._to_addrs)

    async def deliver(
        self,
        severity: Severity,
        payload: Payload,
        silent: bool,
    ) -> AdapterDeliverResult:
        del severity, silent  # see class docstring; silent is a no-op
        msg = EmailMessage()
        msg["From"] = self._from_addr
        msg["To"] = ", ".join(self._to_addrs)
        msg["Subject"] = payload.title
        body = payload.body
        if payload.url is not None:
            label = payload.url_title or payload.url
            body += f"\n\n{label}: {payload.url}"
        msg.set_content(body)

        try:
            await aiosmtplib.send(
                msg,
                hostname=self._host,
                port=self._port,
                username=self._username,
                password=self._password,
                use_tls=self._use_tls,
                start_tls=self._start_tls,
                timeout=self._timeout,
            )
        except aiosmtplib.SMTPAuthenticationError:
            return AdapterDeliverResult(
                status=AdapterStatus.PERMANENT_FAILURE,
                error_code=ErrorCode.AUTH_FAILED,
                detail=_GENERIC_AUTH,
            )
        except (aiosmtplib.SMTPRecipientsRefused, aiosmtplib.SMTPSenderRefused):
            return AdapterDeliverResult(
                status=AdapterStatus.PERMANENT_FAILURE,
                error_code=ErrorCode.ADAPTER_INVALID_PAYLOAD,
                detail=_GENERIC_FAILURE,
            )
        except TimeoutError:
            return AdapterDeliverResult(
                status=AdapterStatus.RETRIABLE_FAILURE,
                error_code=ErrorCode.TIMEOUT,
                detail=_GENERIC_FAILURE,
            )
        except (aiosmtplib.SMTPException, OSError):
            return AdapterDeliverResult(
                status=AdapterStatus.RETRIABLE_FAILURE,
                error_code=ErrorCode.UPSTREAM_ERROR,
                detail=_GENERIC_FAILURE,
            )

        return AdapterDeliverResult(status=AdapterStatus.DELIVERED)
