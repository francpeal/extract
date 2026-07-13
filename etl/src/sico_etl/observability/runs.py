from __future__ import annotations

from typing import Any

from sico_etl.domain.models import EntityMetrics, Run


class PostgresRunStore:
    def __init__(self, connection: Any) -> None:
        self._connection = connection

    def start(self, run: Run, mode: str) -> None:
        with self._connection.cursor() as cursor:
            cursor.execute(
                """
                INSERT INTO etl_runs (id, status, mode, started_at, etl_version)
                VALUES (%s, %s, %s, %s, %s)
                """,
                (run.id, run.status.value, mode, run.started_at, "0.1.0"),
            )
        self._connection.commit()

    def record_entity(self, run: Run, metrics: EntityMetrics) -> None:
        with self._connection.cursor() as cursor:
            cursor.execute(
                """
                INSERT INTO etl_entity_runs
                    (run_id, entity, status, started_at, finished_at, pages,
                     rows_read, rows_inserted, rows_updated, rows_unchanged,
                     rows_rejected, rows_deactivated)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (
                    run.id,
                    metrics.entity.value,
                    "succeeded",
                    metrics.started_at,
                    metrics.finished_at,
                    metrics.pages,
                    metrics.rows_read,
                    metrics.rows_inserted,
                    metrics.rows_updated,
                    metrics.rows_unchanged,
                    metrics.rows_rejected,
                    metrics.rows_deactivated,
                ),
            )
        self._connection.commit()

    def finish(self, run: Run) -> None:
        with self._connection.cursor() as cursor:
            cursor.execute(
                """
                UPDATE etl_runs
                   SET status = %s, finished_at = %s, error_summary = %s
                 WHERE id = %s
                """,
                (run.status.value, run.finished_at, run.error_summary, run.id),
            )
        self._connection.commit()
