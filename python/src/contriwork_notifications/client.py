"""NotificationClient — multicast orchestrator over a list of adapters.

CONTRACT.md §Multicast semantics, §Quiet hours, §Rate limiting, §Retry.
"""

from __future__ import annotations

import asyncio
import random
from collections.abc import Sequence

from ._quiet_hours import is_quiet
from ._rate_limit import RateLimiter
from .adapter import Adapter, AdapterDeliverResult, AdapterStatus
from .config import NotificationConfig, RetryConfig
from .errors import ErrorCode
from .payload import Payload, validate
from .result import AdapterOutcome, OutcomeStatus, SendResult
from .severity import Severity

_DEFAULT_RETRY = RetryConfig()


class NotificationClient:
    """Transport-only orchestrator: multicast delivery to every adapter in parallel.

    The client never persists messages, deduplicates, routes by named channels,
    or owns configuration sources. Anything beyond send / retry / quiet-hours
    silent downgrade / per-adapter business rate limit is the consumer's
    responsibility.
    """

    def __init__(
        self,
        adapters: Sequence[Adapter],
        config: NotificationConfig | None = None,
    ) -> None:
        self._adapters: list[Adapter] = list(adapters)
        self._config = config if config is not None else NotificationConfig()
        self._retry = self._config.retry or _DEFAULT_RETRY
        self._rate_limiter = RateLimiter(self._config.rate_limits)

    async def send(self, severity: Severity, payload: Payload) -> SendResult:
        # 1. Validate payload — short-circuit before any adapter is invoked.
        if validate(payload) is not None:
            return SendResult(
                ok=False,
                results=[],
                error_code=ErrorCode.INVALID_PAYLOAD,
                attempts=0,
            )

        # 2. Empty adapter list — explicit no-op.
        if not self._adapters:
            return SendResult(ok=True, results=[], error_code=None, attempts=0)

        # 3. Decide silent mode once for this send.
        silent = self._should_silence(severity)

        # 4. Multicast: invoke every adapter in parallel.
        outcomes = await asyncio.gather(
            *(self._invoke_adapter(a, severity, payload, silent) for a in self._adapters)
        )

        total_attempts = sum(o.attempts for o in outcomes)
        any_delivered = any(
            o.status in (OutcomeStatus.DELIVERED, OutcomeStatus.DELIVERED_SILENT) for o in outcomes
        )

        if any_delivered:
            return SendResult(
                ok=True,
                results=list(outcomes),
                error_code=None,
                attempts=total_attempts,
            )
        return SendResult(
            ok=False,
            results=list(outcomes),
            error_code=ErrorCode.ALL_ADAPTERS_FAILED,
            attempts=total_attempts,
        )

    # ---- internals ---------------------------------------------------------

    def _should_silence(self, severity: Severity) -> bool:
        qh = self._config.quiet_hours
        if qh is None:
            return False
        if severity in qh.bypass_severities:
            return False
        return is_quiet(qh)

    async def _invoke_adapter(
        self,
        adapter: Adapter,
        severity: Severity,
        payload: Payload,
        silent: bool,
    ) -> AdapterOutcome:
        # 4a. Business rate limit (configured policy).
        if not self._is_rate_limit_exempt(adapter, severity) and not self._rate_limiter.allow(
            adapter.name
        ):
            return AdapterOutcome(
                adapter=adapter.name,
                status=OutcomeStatus.BUSINESS_RATE_LIMITED,
                attempts=0,
                error_code=ErrorCode.BUSINESS_RATE_LIMITED,
                detail=None,
            )

        # 4b. Availability precheck (creds, platform, etc.).
        try:
            available = await adapter.is_available()
        except Exception:
            available = False
        if not available:
            return AdapterOutcome(
                adapter=adapter.name,
                status=OutcomeStatus.PERMANENT_FAILURE,
                attempts=1,
                error_code=ErrorCode.ADAPTER_UNAVAILABLE,
                detail=None,
            )

        # 4c. Deliver with retry on RETRIABLE_FAILURE.
        last: AdapterDeliverResult | None = None
        attempts = 0
        for attempt_index in range(self._retry.max_attempts):
            attempts += 1
            try:
                last = await adapter.deliver(severity, payload, silent)
            except Exception:
                last = AdapterDeliverResult(
                    status=AdapterStatus.RETRIABLE_FAILURE,
                    error_code=ErrorCode.UPSTREAM_ERROR,
                    detail=None,
                )

            if last.status == AdapterStatus.DELIVERED:
                outcome_status = (
                    OutcomeStatus.DELIVERED_SILENT if silent else OutcomeStatus.DELIVERED
                )
                return AdapterOutcome(
                    adapter=adapter.name,
                    status=outcome_status,
                    attempts=attempts,
                    error_code=None,
                    detail=last.detail,
                )
            if last.status == AdapterStatus.PERMANENT_FAILURE:
                return AdapterOutcome(
                    adapter=adapter.name,
                    status=OutcomeStatus.PERMANENT_FAILURE,
                    attempts=attempts,
                    error_code=last.error_code,
                    detail=last.detail,
                )
            # RETRIABLE_FAILURE: wait if attempts remain.
            if attempts < self._retry.max_attempts:
                await asyncio.sleep(self._compute_delay(attempt_index))

        # All attempts exhausted with RETRIABLE_FAILURE.
        assert last is not None
        return AdapterOutcome(
            adapter=adapter.name,
            status=OutcomeStatus.RETRIABLE_FAILURE,
            attempts=attempts,
            error_code=last.error_code,
            detail=last.detail,
        )

    def _is_rate_limit_exempt(self, adapter: Adapter, severity: Severity) -> bool:
        policy = self._rate_limiter.policy_for(adapter.name)
        if policy is None:
            return True
        return severity in policy.bypass_severities

    def _compute_delay(self, attempt_index: int) -> float:
        # delay(n) = min(max_delay_ms, base_delay_ms * 2^n) * (1 ± jitter)
        base = self._retry.base_delay_ms * (2**attempt_index)
        capped: float = float(min(base, self._retry.max_delay_ms))
        if self._retry.jitter_ratio > 0:
            # Pseudo-random jitter; statistical distribution is sufficient,
            # not used for cryptography.
            jitter = capped * self._retry.jitter_ratio * (random.random() * 2 - 1)  # noqa: S311
            capped = max(0.0, capped + jitter)
        return capped / 1000.0
