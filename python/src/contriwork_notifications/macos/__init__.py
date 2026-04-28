"""macOS-only adapters — opt-in import surface.

Imported separately from the package's main public surface because the
macOS adapters depend on platform features (``osascript``, the iMessage
``Messages.app``) that are not available on Linux or Windows. Importing
this module on a non-macOS host succeeds, but the adapters report
``ADAPTER_UNAVAILABLE`` at runtime.
"""

from __future__ import annotations

from .imessage import IMessageAdapter

__all__ = ["IMessageAdapter"]
