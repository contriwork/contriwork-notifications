"""macOS iMessage adapter — outbound only, Python-only.

CONTRACT.md §Security invariants:
  * AppleScript injection prevention (PTV3-04 mitigation): every value
    interpolated into the ``osascript`` payload is escaped before being
    embedded as an AppleScript string literal. Backslash MUST be escaped
    first; backtick is dropped to remove a shell-exec vector. Tab and
    newlines are normalised because they would otherwise break the
    ``send "..." to targetBuddy`` form.
  * Subprocess timeout: a hard 10-second timeout is enforced on the
    ``osascript`` invocation; expiry is reported as ``TIMEOUT``.

Inbound iMessage (chat.db read, ``/command`` parsing) is intentionally
out of scope; see docs/SCOPE.md.
"""

from __future__ import annotations

import asyncio
import platform
import subprocess

from ..adapter import AdapterDeliverResult, AdapterStatus
from ..errors import ErrorCode
from ..payload import Payload
from ..severity import Severity

_DEFAULT_TIMEOUT_S = 10.0
_GENERIC_FAILURE = "Notification delivery failed"


def _applescript_escape(value: str) -> str:
    """Escape ``value`` so it can be embedded as an AppleScript string literal.

    Order matters: backslash MUST be escaped first so subsequent escapes
    do not double-escape the inserted backslashes.
    """
    value = value.replace("\\", "\\\\")
    value = value.replace('"', '\\"')
    value = value.replace("`", "")  # backtick: shell-exec risk inside AppleScript do shell script
    value = value.replace("\r", "")
    value = value.replace("\n", " ")
    value = value.replace("\t", " ")
    return value


class IMessageAdapter:
    """macOS iMessage outbound adapter.

    Sends a single iMessage via ``osascript`` to a fixed recipient
    (phone number or Apple ID). The recipient is supplied at construction;
    multi-recipient fan-out is the consumer's job.

    iMessage has no programmatic silent flag, so ``silent`` is a documented
    no-op (CONTRACT.md §Quiet hours flags this explicitly).
    """

    def __init__(
        self,
        recipient: str,
        *,
        name: str = "imessage",
        timeout_seconds: float = _DEFAULT_TIMEOUT_S,
    ) -> None:
        self._recipient = recipient
        self.name = name
        self._timeout = timeout_seconds

    async def is_available(self) -> bool:
        return platform.system() == "Darwin" and bool(self._recipient)

    async def deliver(
        self,
        severity: Severity,
        payload: Payload,
        silent: bool,
    ) -> AdapterDeliverResult:
        del severity, silent  # see class docstring; silent is a no-op
        if platform.system() != "Darwin":
            return AdapterDeliverResult(
                status=AdapterStatus.PERMANENT_FAILURE,
                error_code=ErrorCode.ADAPTER_UNAVAILABLE,
                detail=_GENERIC_FAILURE,
            )

        full_message = f"[{payload.title}] {payload.body}"
        recipient = _applescript_escape(self._recipient)
        message = _applescript_escape(full_message)
        script = (
            'tell application "Messages"\n'
            "    set targetService to 1st account whose service type = iMessage\n"
            f'    set targetBuddy to participant "{recipient}" of targetService\n'
            f'    send "{message}" to targetBuddy\n'
            "end tell"
        )

        try:
            result = await asyncio.to_thread(
                subprocess.run,
                ["osascript", "-e", script],
                capture_output=True,
                text=True,
                timeout=self._timeout,
                check=False,
            )
        except subprocess.TimeoutExpired:
            return AdapterDeliverResult(
                status=AdapterStatus.RETRIABLE_FAILURE,
                error_code=ErrorCode.TIMEOUT,
                detail=_GENERIC_FAILURE,
            )
        except OSError:
            return AdapterDeliverResult(
                status=AdapterStatus.PERMANENT_FAILURE,
                error_code=ErrorCode.ADAPTER_UNAVAILABLE,
                detail=_GENERIC_FAILURE,
            )

        if result.returncode == 0:
            return AdapterDeliverResult(status=AdapterStatus.DELIVERED)
        return AdapterDeliverResult(
            status=AdapterStatus.PERMANENT_FAILURE,
            error_code=ErrorCode.UPSTREAM_ERROR,
            detail=_GENERIC_FAILURE,
        )
