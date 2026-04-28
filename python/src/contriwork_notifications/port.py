"""Public port: NotificationPort Protocol. CONTRACT.md §Port."""

from __future__ import annotations

from typing import Protocol, runtime_checkable

from .payload import Payload
from .result import SendResult
from .severity import Severity


@runtime_checkable
class NotificationPort(Protocol):
    """Transport-only notification port.

    The concrete implementation is :class:`contriwork_notifications.NotificationClient`,
    which also satisfies this Protocol so callers can type against the
    abstraction.
    """

    async def send(self, severity: Severity, payload: Payload) -> SendResult:
        """Multicast the payload to every configured adapter in parallel.

        See CONTRACT.md §Methods → ``send`` for the full semantics.
        """
        ...
