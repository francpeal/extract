from dataclasses import replace
from datetime import datetime, timezone
import os
import unittest
from uuid import uuid4

from sico_etl.destination.repositories import PostgresEntityRepository
from sico_etl.domain.contracts import CONTRACTS
from sico_etl.domain.models import Entity


@unittest.skipUnless(
    os.getenv("TEST_POSTGRES_DSN"),
    "TEST_POSTGRES_DSN is required for isolated PostgreSQL integration tests",
)
class PostgresRepositoryIntegrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        import psycopg

        cls.connection = psycopg.connect(os.environ["TEST_POSTGRES_DSN"])
        cls.table = f"etl_test_articles_{uuid4().hex}"
        with cls.connection.cursor() as cursor:
            cursor.execute(
                f"""
                CREATE TABLE {cls.table}
                (
                    codigo varchar(20) PRIMARY KEY,
                    activo boolean NOT NULL,
                    updated_at timestamptz NOT NULL
                )
                """
            )
        cls.connection.commit()
        source = CONTRACTS[Entity.ARTICLES]
        cls.contract = replace(
            source,
            destination_table=cls.table,
            destination_mapping={"articleCode": "codigo", "active": "activo"},
            natural_key_candidates=("codigo",),
            mapping_confirmed=True,
        )

    @classmethod
    def tearDownClass(cls) -> None:
        with cls.connection.cursor() as cursor:
            cursor.execute(f"DROP TABLE IF EXISTS {cls.table}")
        cls.connection.commit()
        cls.connection.close()

    def test_insert_unchanged_and_update_are_idempotent(self) -> None:
        repository = PostgresEntityRepository(
            self.connection,
            self.contract,
            min_expected_rows=1,
            max_decrease_percent=100,
        )
        timestamp = datetime(2026, 7, 13, tzinfo=timezone.utc)

        inserted = repository.publish(
            [{"articleCode": "ART-001", "active": True, "sourceUpdatedAt": timestamp}]
        )
        unchanged = repository.publish(
            [{"articleCode": "ART-001", "active": True, "sourceUpdatedAt": timestamp}]
        )
        updated = repository.publish(
            [{"articleCode": "ART-001", "active": False, "sourceUpdatedAt": timestamp}]
        )

        self.assertEqual(inserted.inserted, 1)
        self.assertEqual(unchanged.unchanged, 1)
        self.assertEqual(updated.updated, 1)


if __name__ == "__main__":
    unittest.main()
