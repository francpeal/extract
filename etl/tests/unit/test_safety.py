import os
import unittest
from dataclasses import replace
from unittest.mock import patch

from sico_etl.config import ConfigurationError, Settings
from sico_etl.destination.repositories import MappingNotConfirmedError, PostgresEntityRepository
from sico_etl.domain.contracts import CONTRACTS
from sico_etl.domain.models import Entity


class SafetyTests(unittest.TestCase):
    def test_non_loopback_winbridge_url_is_rejected(self) -> None:
        with patch.dict(os.environ, {"WINBRIDGE_BASE_URL": "http://192.0.2.1:5000"}, clear=True):
            with self.assertRaisesRegex(ConfigurationError, "loopback"):
                Settings.from_env()

    def test_loopback_prefix_cannot_hide_remote_host(self) -> None:
        malicious = "http://127.0.0.1:15000@evil.example:80"
        with patch.dict(os.environ, {"WINBRIDGE_BASE_URL": malicious}, clear=True):
            with self.assertRaisesRegex(ConfigurationError, "loopback"):
                Settings.from_env()

    def test_unconfirmed_mapping_blocks_before_database_use(self) -> None:
        contract = replace(CONTRACTS[Entity.ARTICLES], mapping_confirmed=False)
        repository = PostgresEntityRepository(object(), contract)
        with self.assertRaisesRegex(MappingNotConfirmedError, "not confirmed"):
            repository.publish([{"articleCode": "A", "active": True}])


if __name__ == "__main__":
    unittest.main()
