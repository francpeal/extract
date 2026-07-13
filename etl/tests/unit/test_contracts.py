from datetime import datetime, timezone
from decimal import Decimal
import unittest

from sico_etl.domain.contracts import CONTRACTS, ContractError
from sico_etl.domain.models import Entity


class ContractTests(unittest.TestCase):
    def test_price_preserves_decimal(self) -> None:
        item = {
            "articleCode": "ART-001",
            "priceListCode": "001",
            "priceUsd": Decimal("12.34"),
            "pricePen": Decimal("45.67"),
            "sourceUpdatedAt": datetime(2026, 7, 13, tzinfo=timezone.utc),
        }

        validated = CONTRACTS[Entity.PRICES].validate(item)

        self.assertEqual(validated["priceUsd"], Decimal("12.34"))

    def test_required_field_is_rejected(self) -> None:
        with self.assertRaisesRegex(ContractError, "articleCode"):
            CONTRACTS[Entity.ARTICLES].validate({"active": True})

    def test_boolean_does_not_pass_as_decimal_integer(self) -> None:
        item = {
            "articleCode": "ART-001",
            "priceListCode": "001",
            "priceUsd": True,
            "pricePen": Decimal("1"),
        }
        with self.assertRaisesRegex(ContractError, "priceUsd"):
            CONTRACTS[Entity.PRICES].validate(item)

    def test_code_length_is_enforced(self) -> None:
        with self.assertRaisesRegex(ContractError, "exceeds 20"):
            CONTRACTS[Entity.ARTICLES].validate(
                {"articleCode": "X" * 21, "active": True}
            )

    def test_source_timestamp_requires_timezone(self) -> None:
        with self.assertRaisesRegex(ContractError, "timezone"):
            CONTRACTS[Entity.ARTICLES].validate(
                {
                    "articleCode": "ART-001",
                    "active": True,
                    "sourceUpdatedAt": datetime(2026, 7, 13),
                }
            )

    def test_non_finite_decimal_is_rejected(self) -> None:
        with self.assertRaisesRegex(ContractError, "finite decimal"):
            CONTRACTS[Entity.PRICES].validate(
                {
                    "articleCode": "ART-001",
                    "priceListCode": "001",
                    "priceUsd": Decimal("NaN"),
                    "pricePen": Decimal("1"),
                }
            )

    def test_all_mappings_are_deliberately_unconfirmed(self) -> None:
        self.assertTrue(CONTRACTS)
        self.assertTrue(all(not contract.mapping_confirmed for contract in CONTRACTS.values()))


if __name__ == "__main__":
    unittest.main()
