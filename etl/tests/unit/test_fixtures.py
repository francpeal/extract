from pathlib import Path
import json
import unittest

from sico_etl.domain.models import Entity
from sico_etl.source.client import TransportResponse
from sico_etl.source.winbridge import WinBridgeClient


class OneResponseTransport:
    def __init__(self, body: bytes) -> None:
        self.body = body

    def get(self, url: str, timeout: int) -> TransportResponse:
        return TransportResponse(200, self.body)


class FixtureTests(unittest.TestCase):
    def test_all_entity_fixtures_match_their_contract(self) -> None:
        fixture_path = Path(__file__).parents[1] / "fixtures" / "entities.json"
        fixtures = json.loads(fixture_path.read_text(encoding="utf-8"))
        for entity in Entity:
            with self.subTest(entity=entity.value):
                body = json.dumps(
                    {
                        "items": [fixtures[entity.value]],
                        "nextCursor": None,
                        "extractedAt": "2026-07-13T12:00:00Z",
                    }
                ).encode()
                client = WinBridgeClient(
                    "http://127.0.0.1:15000",
                    timeout_seconds=5,
                    page_limit=10,
                    max_retries=1,
                    retry_base_seconds=0.1,
                    transport=OneResponseTransport(body),
                )
                pages = list(client.iter_pages(entity))
                self.assertEqual(len(pages[0].items), 1)


if __name__ == "__main__":
    unittest.main()
