"""Payload schema and validation. CONTRACT.md §Payload schema."""

from __future__ import annotations

from collections.abc import Mapping
from dataclasses import dataclass

_TITLE_MAX = 200
_BODY_MAX = 2000
_URL_TITLE_MAX = 100
_HTTPS_PREFIX = "https://"


@dataclass(frozen=True, slots=True)
class Payload:
    """Notification payload.

    Validation rules (enforced by NotificationClient before any adapter is invoked):
      * title: non-empty, <= 200 chars after trim
      * body: non-empty, <= 2000 chars after trim
      * url: optional; if present, MUST start with ``https://``
      * url_title: optional; <= 100 chars; ignored when url is None
      * metadata: optional; opaque to the port, passed through to adapters
    """

    title: str
    body: str
    url: str | None = None
    url_title: str | None = None
    metadata: Mapping[str, str] | None = None


def validate(payload: Payload) -> str | None:
    """Return None if valid, otherwise a short reason string.

    The reason string is informational; the public surface always reports the
    failure as the stable error code ``INVALID_PAYLOAD`` and never echoes the
    reason verbatim to callers (no secret leakage by construction).
    """
    title = payload.title.strip() if payload.title else ""
    if not title:
        return "title is empty"
    if len(title) > _TITLE_MAX:
        return f"title exceeds {_TITLE_MAX} char cap"

    body = payload.body.strip() if payload.body else ""
    if not body:
        return "body is empty"
    if len(body) > _BODY_MAX:
        return f"body exceeds {_BODY_MAX} char cap"

    if payload.url is not None and not payload.url.startswith(_HTTPS_PREFIX):
        return "url must use https scheme"

    if payload.url_title is not None and len(payload.url_title) > _URL_TITLE_MAX:
        return f"url_title exceeds {_URL_TITLE_MAX} char cap"

    return None
