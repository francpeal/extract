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

    def test_price_uses_confirmed_composite_key(self) -> None:
        self.assertEqual(
            CONTRACTS[Entity.PRICES].natural_key_candidates,
            ("cod_articulo", "cod_lista"),
        )

    def test_warehouse_stock_uses_confirmed_composite_key(self) -> None:
        self.assertEqual(
            CONTRACTS[Entity.WAREHOUSE_STOCK].natural_key_candidates,
            ("cod_articulo", "cod_almacen"),
        )

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

    def test_all_mappings_are_enabled_for_operational_publication(self) -> None:
        self.assertTrue(CONTRACTS)
        self.assertTrue(all(contract.mapping_confirmed for contract in CONTRACTS.values()))

    def test_customer_uses_confirmed_code_candidate_only(self) -> None:
        self.assertEqual(
            CONTRACTS[Entity.CUSTOMERS].natural_key_candidates,
            ("cod_dap",),
        )

    def test_price_list_code_matches_price_reference_length(self) -> None:
        with self.assertRaisesRegex(ContractError, "exceeds 3"):
            CONTRACTS[Entity.PRICE_LISTS].validate(
                {"priceListCode": "0001", "name": "Lista", "active": True}
            )

    def test_article_mapping_preserves_web_owned_fields(self) -> None:
        destinations = set(CONTRACTS[Entity.ARTICLES].destination_mapping.values())
        self.assertTrue(
            {"descripcion_comercial", "categoria", "imagen_url"}.isdisjoint(destinations)
        )

    def test_warehouse_code_matches_stock_reference_length(self) -> None:
        with self.assertRaisesRegex(ContractError, "exceeds 10"):
            CONTRACTS[Entity.WAREHOUSES].validate(
                {
                    "warehouseCode": "W" * 11,
                    "name": "Almacén",
                    "active": True,
                }
            )

    def test_warehouse_destination_lengths_are_enforced(self) -> None:
        with self.assertRaisesRegex(ContractError, "name exceeds 100"):
            CONTRACTS[Entity.WAREHOUSES].validate(
                {
                    "warehouseCode": "001",
                    "name": "W" * 101,
                    "active": True,
                }
            )
        with self.assertRaisesRegex(ContractError, "abbreviation exceeds 10"):
            CONTRACTS[Entity.WAREHOUSES].validate(
                {
                    "warehouseCode": "001",
                    "name": "Almacén",
                    "abbreviation": "W" * 11,
                    "active": True,
                }
            )

    def test_price_list_destination_name_length_is_enforced(self) -> None:
        with self.assertRaisesRegex(ContractError, "name exceeds 100"):
            CONTRACTS[Entity.PRICE_LISTS].validate(
                {
                    "priceListCode": "001",
                    "name": "L" * 101,
                    "active": True,
                }
            )


if __name__ == "__main__":
    unittest.main()
