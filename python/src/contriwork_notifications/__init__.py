"""contriwork-notifications — Python implementation.

Public surface re-exported here mirrors CONTRACT.md. All other modules are
implementation detail and may change without a contract bump.
"""

from __future__ import annotations

from importlib.metadata import PackageNotFoundError, version

from .adapter import Adapter, AdapterDeliverResult, AdapterStatus
from .adapters import (
    DiscordWebhookAdapter,
    InMemoryAdapter,
    PushoverAdapter,
    SlackWebhookAdapter,
    TelegramAdapter,
)
from .client import NotificationClient
from .config import (
    NotificationConfig,
    QuietHoursConfig,
    RateLimitPolicy,
    RetryConfig,
)
from .errors import ErrorCode
from .payload import Payload
from .port import NotificationPort
from .result import AdapterOutcome, OutcomeStatus, SendResult
from .severity import Severity

__all__ = [
    "Adapter",
    "AdapterDeliverResult",
    "AdapterOutcome",
    "AdapterStatus",
    "DiscordWebhookAdapter",
    "ErrorCode",
    "InMemoryAdapter",
    "NotificationClient",
    "NotificationConfig",
    "NotificationPort",
    "OutcomeStatus",
    "Payload",
    "PushoverAdapter",
    "QuietHoursConfig",
    "RateLimitPolicy",
    "RetryConfig",
    "SendResult",
    "Severity",
    "SlackWebhookAdapter",
    "TelegramAdapter",
    "__version__",
]

try:
    __version__: str = version("contriwork-notifications")
except PackageNotFoundError:  # pragma: no cover
    __version__ = "0.0.0"
