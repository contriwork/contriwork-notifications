"""Quiet-hours window evaluation (internal helper)."""

from __future__ import annotations

from datetime import datetime, time
from zoneinfo import ZoneInfo

from .config import QuietHoursConfig


def _parse_hhmm(s: str) -> time:
    h_str, m_str = s.split(":", 1)
    return time(hour=int(h_str), minute=int(m_str))


def is_quiet(cfg: QuietHoursConfig, now: datetime | None = None) -> bool:
    """Return True if ``now`` (or wall-clock now) is inside the quiet window."""
    tz = ZoneInfo(cfg.timezone)
    moment = (now.astimezone(tz) if now is not None else datetime.now(tz)).time()
    start = _parse_hhmm(cfg.start)
    end = _parse_hhmm(cfg.end)
    if start <= end:
        return start <= moment < end
    # Wraps past midnight
    return moment >= start or moment < end
