from __future__ import annotations

from datetime import datetime
from typing import Protocol

from sico_etl.destination.repositories import PublishResult
from sico_etl.domain.contracts import ContractError
from sico_etl.domain.cancellation import CancellationController
from sico_etl.domain.models import Entity, EntityMetrics
from sico_etl.source.winbridge import WinBridgeClient


class DestinationRepository(Protocol):
    def publish(
        self, items: list[dict[str, object]], full_snapshot: bool = True
    ) -> PublishResult: ...


class EntityPipeline:
    def __init__(
        self,
        client: WinBridgeClient,
        max_rows: int = 1000000,
        cancellation: CancellationController | None = None,
    ) -> None:
        self._client = client
        self._max_rows = max_rows
        self._cancellation = cancellation

    def run(
        self,
        entity: Entity,
        dry_run: bool,
        repository: DestinationRepository | None = None,
        updated_since: datetime | None = None,
    ) -> EntityMetrics:
        if not dry_run and repository is None:
            raise ValueError("repository is required outside dry-run mode")
        if self._cancellation is not None:
            self._cancellation.raise_if_requested()
        metrics = EntityMetrics(entity=entity)
        buffered: list[dict[str, object]] = []
        for page in self._client.iter_pages(entity, updated_since):
            metrics.pages += 1
            metrics.rows_read += len(page.items)
            if metrics.rows_read > self._max_rows:
                raise ContractError(
                    f"{entity.value}: extraction exceeded safety limit {self._max_rows}"
                )
            if not dry_run:
                buffered.extend(page.items)
            if self._cancellation is not None:
                self._cancellation.raise_if_requested()
        if not dry_run:
            assert repository is not None
            result = repository.publish(
                buffered, full_snapshot=updated_since is None
            )
            metrics.rows_inserted = result.inserted
            metrics.rows_updated = result.updated
            metrics.rows_unchanged = result.unchanged
        metrics.finish()
        return metrics
