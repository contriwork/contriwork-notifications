"""Smoke tests — verify package imports and port is reachable."""

from __future__ import annotations


def test_package_imports() -> None:
    import contriwork_notifications

    assert contriwork_notifications.__version__


def test_port_is_exported() -> None:
    from contriwork_notifications import NotificationsPort

    assert NotificationsPort is not None
    assert hasattr(NotificationsPort, "example")
