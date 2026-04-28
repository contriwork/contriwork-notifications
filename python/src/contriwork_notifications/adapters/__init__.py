"""Concrete adapters bundled with the package.

Per-adapter modules live next to this file. Adapters are explicit, opt-in
imports — the package never auto-discovers or instantiates them.
"""

from __future__ import annotations

from .memory import InMemoryAdapter
from .pushover import PushoverAdapter
from .telegram import TelegramAdapter

__all__ = ["InMemoryAdapter", "PushoverAdapter", "TelegramAdapter"]
