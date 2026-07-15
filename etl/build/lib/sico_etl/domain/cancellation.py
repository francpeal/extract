from __future__ import annotations

from threading import Event
import signal


class CancellationRequested(RuntimeError):
    pass


class CancellationController:
    def __init__(self) -> None:
        self._event = Event()

    def install_signal_handlers(self) -> None:
        signal.signal(signal.SIGINT, self._request)
        signal.signal(signal.SIGTERM, self._request)

    def _request(self, signum: int, frame: object) -> None:
        self._event.set()

    def raise_if_requested(self) -> None:
        if self._event.is_set():
            raise CancellationRequested("ETL cancellation requested")

    def wait(self, seconds: float) -> None:
        if self._event.wait(seconds):
            raise CancellationRequested("ETL cancellation requested")
