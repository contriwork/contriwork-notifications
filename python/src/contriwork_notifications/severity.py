"""Severity enum. CONTRACT.md §Severity.

Five levels, fixed order, names and order frozen for contract revision v1.
"""

from __future__ import annotations

from enum import StrEnum


class Severity(StrEnum):
    """Five severity levels. Order and names are frozen for v1."""

    DEBUG = "DEBUG"
    INFO = "INFO"
    WARN = "WARN"
    ERROR = "ERROR"
    CRITICAL = "CRITICAL"

    @property
    def icon(self) -> str:
        """Stable icon for this severity (CONTRACT.md severity table)."""
        return _ICONS[self]


_ICONS: dict[Severity, str] = {
    Severity.DEBUG: "🔍",
    Severity.INFO: "ℹ️",
    Severity.WARN: "⚠️",
    Severity.ERROR: "❌",
    Severity.CRITICAL: "⛔",
}
