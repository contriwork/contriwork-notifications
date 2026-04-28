"""In-memory test/reference adapter driven by a fixed outcome sequence.

This adapter never touches the network. It is the harness used by the
cross-language contract tests and by per-adapter unit tests that need to
exercise orchestrator behavior without coupling to a real channel.
"""

from __future__ import annotations

from collections.abc import Sequence
from dataclasses import dataclass, field

from ..adapter import AdapterDeliverResult, AdapterStatus
from ..payload import Payload
from ..severity import Severity


@dataclass
class InMemoryAdapter:
    """Adapter whose ``deliver`` returns a pre-configured outcome sequence.

    Each call to ``deliver`` consumes the next entry in ``behaviors``. After
    the sequence is exhausted, the last entry repeats (this mirrors the
    contract-tests fixture schema).

    Every invocation is recorded in ``calls`` so tests can assert on the
    observed severity, payload, and silent flag.

    Attributes:
        name: Stable adapter name. Default is ``"memory"`` but tests usually
            override this so multiple instances coexist in one client.
        behaviors: The outcome sequence; if empty, every call returns a
            DELIVERED result.
        available: Value returned by ``is_available``; default ``True``.
        calls: Recorded ``(severity, payload, silent)`` triples, in order.
    """

    name: str = "memory"
    behaviors: Sequence[AdapterDeliverResult] = field(default_factory=list)
    available: bool = True
    calls: list[tuple[Severity, Payload, bool]] = field(default_factory=list)
    _index: int = 0

    async def is_available(self) -> bool:
        return self.available

    async def deliver(
        self,
        severity: Severity,
        payload: Payload,
        silent: bool,
    ) -> AdapterDeliverResult:
        self.calls.append((severity, payload, silent))
        if not self.behaviors:
            return AdapterDeliverResult(status=AdapterStatus.DELIVERED)
        idx = min(self._index, len(self.behaviors) - 1)
        self._index += 1
        return self.behaviors[idx]
