"""Per-adapter sliding-window rate limiter (in-memory, internal helper)."""

from __future__ import annotations

import time as _time
from collections import deque
from collections.abc import Mapping

from .config import RateLimitPolicy


class _SlidingWindow:
    __slots__ = ("_max_count", "_timestamps", "_window")

    def __init__(self, max_count: int, window_seconds: int) -> None:
        self._max_count = max_count
        self._window = window_seconds
        self._timestamps: deque[float] = deque()

    def allow(self) -> bool:
        now = _time.monotonic()
        while self._timestamps and (now - self._timestamps[0]) > self._window:
            self._timestamps.popleft()
        if len(self._timestamps) >= self._max_count:
            return False
        self._timestamps.append(now)
        return True


class RateLimiter:
    """Per-adapter sliding-window rate limiter, indexed by adapter name."""

    def __init__(self, policies: Mapping[str, RateLimitPolicy] | None) -> None:
        self._policies: dict[str, RateLimitPolicy] = dict(policies) if policies else {}
        self._windows: dict[str, _SlidingWindow] = {}

    def policy_for(self, adapter_name: str) -> RateLimitPolicy | None:
        return self._policies.get(adapter_name)

    def allow(self, adapter_name: str) -> bool:
        policy = self._policies.get(adapter_name)
        if policy is None:
            return True
        if policy.max_count <= 0:
            # 0 (and below) is a valid "always block" config used by tests
            # and by callers that want to circuit-break a channel without
            # removing it from the adapter list.
            return False
        window = self._windows.get(adapter_name)
        if window is None:
            window = _SlidingWindow(policy.max_count, policy.window_seconds)
            self._windows[adapter_name] = window
        return window.allow()
