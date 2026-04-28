"""Config schema (typed only; never loaded by the package itself).

Consumers construct these objects from their preferred source (env, file,
contriwork-config-core, DB) and pass them to NotificationClient.
"""

from __future__ import annotations

from collections.abc import Mapping, Sequence
from dataclasses import dataclass

from .severity import Severity

_DEFAULT_BYPASS: tuple[Severity, ...] = (Severity.CRITICAL,)


@dataclass(frozen=True, slots=True)
class RetryConfig:
    """Exponential backoff + jitter policy for retriable adapter failures."""

    max_attempts: int = 3
    base_delay_ms: int = 500
    max_delay_ms: int = 10_000
    jitter_ratio: float = 0.2


@dataclass(frozen=True, slots=True)
class QuietHoursConfig:
    """Quiet-hours window.

    Severities listed in ``bypass_severities`` ignore the window and are
    delivered normally. Non-bypassed severities are delivered in *silent*
    mode (``DELIVERED_SILENT``); they are never dropped.
    """

    start: str  # "HH:MM" wall-clock
    end: str  # "HH:MM" exclusive; wraps past midnight
    timezone: str  # IANA tz name, e.g. "Europe/Istanbul"
    bypass_severities: Sequence[Severity] = _DEFAULT_BYPASS


@dataclass(frozen=True, slots=True)
class RateLimitPolicy:
    """Per-adapter sliding-window business rate limit."""

    max_count: int
    window_seconds: int
    bypass_severities: Sequence[Severity] = _DEFAULT_BYPASS


@dataclass(frozen=True, slots=True)
class NotificationConfig:
    """Top-level config object passed to ``NotificationClient``.

    All fields are optional. ``rate_limits`` maps an adapter ``name`` to a
    policy; adapters not present in the map are unconstrained.
    """

    retry: RetryConfig | None = None
    quiet_hours: QuietHoursConfig | None = None
    rate_limits: Mapping[str, RateLimitPolicy] | None = None
