"""Adapter protocol. CONTRACT.md §Adapter protocol."""

from __future__ import annotations

from dataclasses import dataclass
from enum import StrEnum
from typing import Protocol, runtime_checkable

from .errors import ErrorCode
from .payload import Payload
from .severity import Severity


class AdapterStatus(StrEnum):
    """Outcome of a single ``deliver()`` invocation (no retry semantics)."""

    DELIVERED = "DELIVERED"
    RETRIABLE_FAILURE = "RETRIABLE_FAILURE"
    PERMANENT_FAILURE = "PERMANENT_FAILURE"


@dataclass(frozen=True, slots=True)
class AdapterDeliverResult:
    """Result of one ``Adapter.deliver`` call."""

    status: AdapterStatus
    error_code: ErrorCode | None = None
    detail: str | None = None


@runtime_checkable
class Adapter(Protocol):
    """Language-agnostic adapter shape. Concrete adapters implement this Protocol.

    Implementations MUST:
      * expose a stable string ``name`` (e.g., ``"pushover"``, ``"slack-webhook"``)
      * return cheaply from ``is_available()`` (no network in the common case)
      * translate ``silent=True`` into the channel's nearest equivalent
        silent representation, or document a no-op explicitly
    """

    name: str

    async def is_available(self) -> bool:
        """Cheap precheck: credentials present, platform supported, etc."""
        ...

    async def deliver(
        self,
        severity: Severity,
        payload: Payload,
        silent: bool,
    ) -> AdapterDeliverResult:
        """Send one notification. See class docstring for ``silent`` semantics."""
        ...
