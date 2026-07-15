from __future__ import annotations

from contextlib import contextmanager
from typing import Any, Iterator


class PostgresDependencyError(RuntimeError):
    pass


class PostgresDatabase:
    def __init__(self, dsn: str) -> None:
        self._dsn = dsn

    @contextmanager
    def connect(self) -> Iterator[Any]:
        try:
            import psycopg
        except ImportError as exc:
            raise PostgresDependencyError(
                "Install the project dependencies before writing to PostgreSQL"
            ) from exc
        with psycopg.connect(self._dsn) as connection:
            yield connection

    def acquire_lock(self, connection: Any, lock_id: int) -> bool:
        with connection.cursor() as cursor:
            cursor.execute("SELECT pg_try_advisory_lock(%s)", (lock_id,))
            row = cursor.fetchone()
            return bool(row and row[0])

    def release_lock(self, connection: Any, lock_id: int) -> None:
        with connection.cursor() as cursor:
            cursor.execute("SELECT pg_advisory_unlock(%s)", (lock_id,))
