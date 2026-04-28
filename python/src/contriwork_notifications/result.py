"""Send-result types. CONTRACT.md §Send result."""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import StrEnum

from .errors import ErrorCode


class OutcomeStatus(StrEnum):
    """Per-adapter outcome status as reported by the client orchestrator."""

    DELIVERED = "DELIVERED"
    DELIVERED_SILENT = "DELIVERED_SILENT"
    RETRIABLE_FAILURE = "RETRIABLE_FAILURE"
    PERMANENT_FAILURE = "PERMANENT_FAILURE"
    BUSINESS_RATE_LIMITED = "BUSINESS_RATE_LIMITED"


@dataclass(frozen=True, slots=True)
class AdapterOutcome:
    """The aggregate outcome reported for a single adapter, after retries."""

    adapter: str
    status: OutcomeStatus
    attempts: int
    error_code: ErrorCode | None = None
    detail: str | None = None


@dataclass(frozen=True, slots=True)
class SendResult:
    """Result of a single ``NotificationClient.send`` call."""

    ok: bool
    results: list[AdapterOutcome] = field(default_factory=list)
    error_code: ErrorCode | None = None
    attempts: int = 0
