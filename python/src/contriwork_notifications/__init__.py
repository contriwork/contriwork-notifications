"""contriwork-notifications — Python adapter.

Public surface re-exports from :mod:`contriwork_notifications.port`. Do not
import concrete adapter classes from outside — they are internal detail.
"""

from __future__ import annotations

from importlib.metadata import PackageNotFoundError, version

from .port import NotificationsPort

__all__ = ["NotificationsPort", "__version__"]

try:
    __version__ = version("contriwork-notifications")
except PackageNotFoundError:
    __version__ = "0.0.0"
