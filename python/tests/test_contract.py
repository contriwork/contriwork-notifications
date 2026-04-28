"""Cross-language contract test runner.

Loads ``contract-tests/test_cases.json`` from the repo root and asserts each
fixture's expected_output (or expected_error) matches the result that
``NotificationClient`` produces when driven with ``InMemoryAdapter``
instances built from the per-fixture behavior sequence.

The same JSON drives the C# (xUnit) and TypeScript (vitest) runners; if a
behavior here changes, all three runners change in the same PR.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import pytest

from contriwork_notifications import (
    AdapterDeliverResult,
    AdapterStatus,
    ErrorCode,
    InMemoryAdapter,
    NotificationClient,
    NotificationConfig,
    OutcomeStatus,
    Payload,
    QuietHoursConfig,
    RateLimitPolicy,
    RetryConfig,
    Severity,
)

FIXTURE = Path(__file__).resolve().parents[2] / "contract-tests" / "test_cases.json"


def _load_cases() -> list[dict[str, Any]]:
    data = json.loads(FIXTURE.read_text(encoding="utf-8"))
    assert data["schema_version"] == 1, (
        "contract fixture schema_version changed -- update all three language runners. "
        f"Got {data['schema_version']}"
    )
    assert data["contract_revision"] == "v1", (
        "contract_revision drifted from v1 -- update CONTRACT.md and runners together. "
        f"Got {data['contract_revision']}"
    )
    cases: list[dict[str, Any]] = data.get("cases", [])
    return [c for c in cases if "python" not in c.get("skip_languages", [])]


def _build_adapter(spec: dict[str, Any]) -> InMemoryAdapter:
    sequence = spec.get("behavior", {}).get("sequence", [])
    behaviors = [
        AdapterDeliverResult(
            status=AdapterStatus(entry["status"]),
            error_code=ErrorCode(entry["error_code"]) if entry.get("error_code") else None,
            detail=entry.get("detail"),
        )
        for entry in sequence
    ]
    return InMemoryAdapter(name=spec["name"], behaviors=behaviors)


def _build_config(spec: dict[str, Any]) -> NotificationConfig:
    retry_spec = spec.get("retry")
    retry = (
        RetryConfig(
            max_attempts=retry_spec["max_attempts"],
            base_delay_ms=retry_spec["base_delay_ms"],
            max_delay_ms=retry_spec["max_delay_ms"],
            jitter_ratio=retry_spec["jitter_ratio"],
        )
        if retry_spec is not None
        else None
    )

    quiet_spec = spec.get("quiet_hours")
    quiet = (
        QuietHoursConfig(
            start=quiet_spec["start"],
            end=quiet_spec["end"],
            timezone=quiet_spec["timezone"],
            bypass_severities=tuple(Severity(s) for s in quiet_spec["bypass_severities"]),
        )
        if quiet_spec is not None
        else None
    )

    rate_spec = spec.get("rate_limits")
    rate_limits = None
    if rate_spec is not None:
        rate_limits = {
            name: RateLimitPolicy(
                max_count=p["max_count"],
                window_seconds=p["window_seconds"],
                bypass_severities=tuple(Severity(s) for s in p["bypass_severities"]),
            )
            for name, p in rate_spec.items()
        }

    return NotificationConfig(retry=retry, quiet_hours=quiet, rate_limits=rate_limits)


def _build_payload(p: dict[str, Any]) -> Payload:
    return Payload(
        title=p["title"],
        body=p["body"],
        url=p.get("url"),
        url_title=p.get("url_title"),
        metadata=p.get("metadata"),
    )


@pytest.mark.contract
def test_fixture_is_well_formed() -> None:
    data = json.loads(FIXTURE.read_text(encoding="utf-8"))
    assert isinstance(data.get("cases"), list)
    assert data.get("contract_revision") == "v1"
    assert data.get("schema_version") == 1
    assert len(data["cases"]) >= 1


@pytest.mark.contract
@pytest.mark.parametrize("case", _load_cases(), ids=lambda c: c["name"])
async def test_case(case: dict[str, Any]) -> None:
    inp = case["input"]
    adapters = [_build_adapter(s) for s in inp["adapters"]]
    config = _build_config(inp["config"])
    client = NotificationClient(adapters=adapters, config=config)

    severity = Severity(inp["send"]["severity"])
    payload = _build_payload(inp["send"]["payload"])
    expected = case["expected_output"]
    label = case["name"]

    result = await client.send(severity, payload)

    assert result.ok is expected["ok"], label
    if expected.get("error_code") is None:
        assert result.error_code is None, label
    else:
        assert result.error_code == expected["error_code"], label
    assert result.attempts == expected["attempts"], label

    expected_results = expected["results"]
    assert len(result.results) == len(expected_results), label

    for actual, exp in zip(result.results, expected_results, strict=True):
        assert actual.adapter == exp["adapter"], label
        assert actual.status == OutcomeStatus(exp["status"]), label
        assert actual.attempts == exp["attempts"], label
        if exp.get("error_code") is None:
            assert actual.error_code is None, label
        else:
            assert actual.error_code == exp["error_code"], label
