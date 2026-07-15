from __future__ import annotations

from dataclasses import dataclass
import json
from typing import Iterable
import unittest

from sico_etl.domain.models import Entity
from sico_etl.source.client import (
    InvalidSourceResponse,
    SourceUnavailable,
    TransportResponse,
)
from sico_etl.source.winbridge import WinBridgeClient


@dataclass
class FakeTransport:
    responses: list[TransportResponse | Exception]

    def get(self, url: str, timeout: int) -> TransportResponse:
        value = self.responses.pop(0)
        if isinstance(value, Exception):
            raise value
        return value


def response(
    items: Iterable[dict[str, object]],
    cursor: str | None = None,
    extracted_at: str = "2026-07-13T12:00:00Z",
) -> TransportResponse:
    payload = {
        "items": list(items),
        "nextCursor": cursor,
        "extractedAt": extracted_at,
    }
    return TransportResponse(200, json.dumps(payload).encode())


def client(transport: FakeTransport, sleeps: list[float] | None = None) -> WinBridgeClient:
    return WinBridgeClient(
        "http://127.0.0.1:15000",
        timeout_seconds=5,
        page_limit=100,
        max_retries=3,
        retry_base_seconds=0.1,
        transport=transport,
        sleeper=(sleeps.append if sleeps is not None else lambda _: None),
        jitter=lambda: 0,
    )


class SourceTests(unittest.TestCase):
    def test_paginates_and_parses_timestamps(self) -> None:
        item = {"warehouseCode": "001", "name": "A", "active": True}
        transport = FakeTransport([response([item], "next"), response([item])])

        pages = list(client(transport).iter_pages(Entity.WAREHOUSES))

        self.assertEqual(len(pages), 2)
        self.assertIsNotNone(pages[0].extracted_at.tzinfo)

    def test_parses_dotnet_seven_digit_fractional_seconds(self) -> None:
        item = {
            "customerCode": "CLI-001",
            "name": "Cliente",
            "legalName": "Cliente S.A.C.",
            "taxId": "20000000001",
            "active": True,
            "sourceCreatedAt": "2026-07-14T13:57:54.1234567-05:00",
        }
        transport = FakeTransport(
            [response([item], extracted_at="2026-07-14T18:57:54.2646986+00:00")]
        )

        page = next(client(transport).iter_pages(Entity.CUSTOMERS))

        self.assertEqual(page.extracted_at.microsecond, 264698)
        self.assertEqual(page.items[0]["sourceCreatedAt"].microsecond, 123456)
        self.assertEqual(page.items[0]["sourceCreatedAt"].utcoffset().total_seconds(), 0)

    def test_retries_retryable_http_status(self) -> None:
        sleeps: list[float] = []
        transport = FakeTransport([TransportResponse(503, b"{}"), response([])])

        pages = list(client(transport, sleeps).iter_pages(Entity.ARTICLES))

        self.assertEqual(len(pages), 1)
        self.assertEqual(sleeps, [0.1])

    def test_retries_transport_failure(self) -> None:
        transport = FakeTransport([SourceUnavailable("offline"), response([])])

        pages = list(client(transport).iter_pages(Entity.ARTICLES))

        self.assertEqual(len(pages), 1)

    def test_rejects_non_retryable_http_status(self) -> None:
        transport = FakeTransport([TransportResponse(400, b"{}")])
        with self.assertRaisesRegex(SourceUnavailable, "non-retryable HTTP 400"):
            list(client(transport).iter_pages(Entity.ARTICLES))

    def test_rejects_cursor_that_does_not_advance(self) -> None:
        item = {"articleCode": "A", "active": True}
        transport = FakeTransport([response([item], "same"), response([item], "same")])
        with self.assertRaisesRegex(InvalidSourceResponse, "did not advance"):
            list(client(transport).iter_pages(Entity.ARTICLES))

    def test_rejects_page_larger_than_requested_limit(self) -> None:
        item = {"articleCode": "A", "active": True}
        transport = FakeTransport([response([item] * 101)])
        with self.assertRaisesRegex(InvalidSourceResponse, "page limit"):
            list(client(transport).iter_pages(Entity.ARTICLES))


if __name__ == "__main__":
    unittest.main()
