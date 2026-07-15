from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any, Iterable

from sico_etl.domain.contracts import EntityContract


class MappingNotConfirmedError(RuntimeError):
    pass


class PublicationSafetyError(RuntimeError):
    pass


@dataclass(frozen=True)
class PublishResult:
    inserted: int
    updated: int
    unchanged: int


class PostgresEntityRepository:
    """Atomic upsert adapter. Disabled until the entity mapping is confirmed."""

    def __init__(
        self,
        connection: Any,
        contract: EntityContract,
        min_expected_rows: int = 1,
        max_decrease_percent: float = 0.0,
    ) -> None:
        self._connection = connection
        self._contract = contract
        self._min_expected_rows = min_expected_rows
        self._max_decrease_percent = max_decrease_percent

    def publish(
        self, items: Iterable[dict[str, Any]], full_snapshot: bool = True
    ) -> PublishResult:
        if not self._contract.mapping_confirmed:
            raise MappingNotConfirmedError(
                f"{self._contract.entity.value}: destination mapping and natural key are not confirmed"
            )
        try:
            from psycopg import sql
        except ImportError as exc:
            raise RuntimeError("psycopg is required for PostgreSQL publication") from exc

        mapped_items = [self._map_item(item) for item in items]
        self._validate_batch(mapped_items, sql, full_snapshot)
        columns = tuple(self._contract.destination_mapping.values()) + ("updated_at",)
        keys = self._contract.natural_key_candidates
        non_keys = tuple(column for column in columns if column not in keys)
        business_columns = tuple(
            column for column in non_keys if column != "updated_at"
        )
        assignments = sql.SQL(", ").join(
            sql.SQL("{} = EXCLUDED.{}").format(sql.Identifier(column), sql.Identifier(column))
            for column in non_keys
        )
        changed = sql.SQL(" OR ").join(
            sql.SQL("{}.{} IS DISTINCT FROM EXCLUDED.{}").format(
                sql.Identifier(self._contract.destination_table),
                sql.Identifier(column),
                sql.Identifier(column),
            )
            for column in business_columns
        )
        statement = sql.SQL(
            "INSERT INTO {} ({}) VALUES ({}) "
            "ON CONFLICT ({}) DO UPDATE SET {} WHERE {} "
            "RETURNING (xmax = 0) AS inserted"
        ).format(
            sql.Identifier(self._contract.destination_table),
            sql.SQL(", ").join(map(sql.Identifier, columns)),
            sql.SQL(", ").join(sql.Placeholder() for _ in columns),
            sql.SQL(", ").join(map(sql.Identifier, keys)),
            assignments,
            changed,
        )
        inserted = updated = unchanged = 0
        with self._connection.transaction():
            with self._connection.cursor() as cursor:
                for item in mapped_items:
                    cursor.execute(statement, tuple(item[column] for column in columns))
                    row = cursor.fetchone()
                    if row is None:
                        unchanged += 1
                    elif row[0]:
                        inserted += 1
                    else:
                        updated += 1
        return PublishResult(inserted, updated, unchanged)

    def _validate_batch(
        self, items: list[dict[str, Any]], sql: Any, full_snapshot: bool
    ) -> None:
        if len(items) < self._min_expected_rows:
            raise PublicationSafetyError(
                f"{self._contract.entity.value}: snapshot contains {len(items)} rows; "
                f"minimum is {self._min_expected_rows}"
            )
        keys = self._contract.natural_key_candidates
        seen: set[tuple[Any, ...]] = set()
        for item in items:
            key = tuple(item.get(column) for column in keys)
            if any(value is None or value == "" for value in key):
                raise PublicationSafetyError(
                    f"{self._contract.entity.value}: null or empty natural key"
                )
            if key in seen:
                raise PublicationSafetyError(
                    f"{self._contract.entity.value}: duplicate natural key in snapshot"
                )
            seen.add(key)
        if not full_snapshot:
            return
        with self._connection.cursor() as cursor:
            cursor.execute(
                sql.SQL("SELECT count(*) FROM {}").format(
                    sql.Identifier(self._contract.destination_table)
                )
            )
            row = cursor.fetchone()
        current_count = int(row[0]) if row else 0
        if current_count > 0:
            decrease = ((current_count - len(items)) / current_count) * 100
            if decrease > self._max_decrease_percent:
                raise PublicationSafetyError(
                    f"{self._contract.entity.value}: snapshot decrease {decrease:.2f}% "
                    f"exceeds {self._max_decrease_percent:.2f}%"
                )

    def _map_item(self, item: dict[str, Any]) -> dict[str, Any]:
        mapped = {
            destination: item.get(source)
            for source, destination in self._contract.destination_mapping.items()
        }
        source_updated_at = item.get("sourceUpdatedAt")
        mapped["updated_at"] = source_updated_at or datetime.now(timezone.utc)
        return mapped
