from unittest.mock import patch
import unittest

from sico_etl.observability.runs import installed_version


class RunStoreTests(unittest.TestCase):
    def test_installed_version_uses_package_metadata(self) -> None:
        with patch("sico_etl.observability.runs.version", return_value="9.8.7"):
            self.assertEqual(installed_version(), "9.8.7")

    def test_installed_version_has_source_fallback(self) -> None:
        from importlib.metadata import PackageNotFoundError

        with patch(
            "sico_etl.observability.runs.version",
            side_effect=PackageNotFoundError,
        ):
            self.assertEqual(installed_version(), "development")


if __name__ == "__main__":
    unittest.main()
