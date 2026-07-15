from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timezone
from decimal import Decimal
from enum import Enum
from typing import Any
from uuid import UUID, uuid4


class Entity(str, Enum):
    WAREHOUSES = "warehouses"
    PRICE_LISTS = "price-lists"
    ARTICLES = "articles"
    CUSTOMERS = "customers"
    PRICES = "prices"
    WAREHOUSE_STOCK = "warehouse-stock"


ENTITY_ORDER = (
    Entity.WAREHOUSES,
    Entity.PRICE_LISTS,
    Entity.ARTICLES,
    Entity.CUSTOMERS,
    Entity.PRICES,
    Entity.WAREHOUSE_STOCK,
)


class RunStatus(str, Enum):
    RUNNING = "running"
    SUCCEEDED = "succeeded"
    FAILED = "failed"
    REJECTED = "rejected"
    DRY_RUN = "dry_run"


@dataclass(frozen=True)
class Page:
    items: tuple[dict[str, Any], ...]
    next_cursor: str | None
    extracted_at: datetime


@dataclass
class EntityMetrics:
    entity: Entity
    rows_read: int = 0
    rows_inserted: int = 0
    rows_updated: int = 0
    rows_unchanged: int = 0
    rows_rejected: int = 0
    rows_deactivated: int = 0
    pages: int = 0
    started_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))
    finished_at: datetime | None = None

    def finish(self) -> None:
        self.finished_at = datetime.now(timezone.utc)


@dataclass
class Run:
    id: UUID = field(default_factory=uuid4)
    status: RunStatus = RunStatus.RUNNING
    started_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))
    finished_at: datetime | None = None
    error_summary: str | None = None

    def finish(self, status: RunStatus, error_summary: str | None = None) -> None:
        self.status = status
        self.error_summary = error_summary
        self.finished_at = datetime.now(timezone.utc)


JsonScalar = str | bool | int | Decimal | None
