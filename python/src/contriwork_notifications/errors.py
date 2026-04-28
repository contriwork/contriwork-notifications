"""Stable error codes. CONTRACT.md §Error taxonomy.

Codes are SCREAMING_SNAKE_CASE strings, never renamed within v1.
"""

from __future__ import annotations

from enum import StrEnum


class ErrorCode(StrEnum):
    """All v1 error codes."""

    # Client-level
    INVALID_PAYLOAD = "INVALID_PAYLOAD"
    BUSINESS_RATE_LIMITED = "BUSINESS_RATE_LIMITED"
    ALL_ADAPTERS_FAILED = "ALL_ADAPTERS_FAILED"

    # Adapter-level
    ADAPTER_UNAVAILABLE = "ADAPTER_UNAVAILABLE"
    RATE_LIMITED = "RATE_LIMITED"
    AUTH_FAILED = "AUTH_FAILED"
    ADAPTER_INVALID_PAYLOAD = "ADAPTER_INVALID_PAYLOAD"
    TIMEOUT = "TIMEOUT"
    UPSTREAM_ERROR = "UPSTREAM_ERROR"
