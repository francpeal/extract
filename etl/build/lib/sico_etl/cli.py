from __future__ import annotations

import argparse
from datetime import datetime
import logging
from typing import Sequence

from sico_etl.config import ConfigurationError, Settings
from sico_etl.destination.postgres import PostgresDatabase, PostgresDependencyError
from sico_etl.destination.repositories import (
    MappingNotConfirmedError,
    PostgresEntityRepository,
    PublicationSafetyError,
)
from sico_etl.domain.contracts import CONTRACTS, ContractError
from sico_etl.domain.cancellation import CancellationController, CancellationRequested
from sico_etl.domain.models import ENTITY_ORDER, Entity, Run, RunStatus
from sico_etl.logging_config import configure_logging
from sico_etl.observability.metrics import metrics_json
from sico_etl.observability.runs import PostgresRunStore
from sico_etl.pipelines import EntityPipeline
from sico_etl.source.client import SourceError
from sico_etl.source.winbridge import WinBridgeClient


LOGGER = logging.getLogger(__name__)


def _parse_timestamp(raw: str) -> datetime:
    value = raw[:-1] + "+00:00" if raw.endswith("Z") else raw
    try:
        parsed = datetime.fromisoformat(value)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("expected an ISO 8601 timestamp") from exc
    if parsed.tzinfo is None:
        raise argparse.ArgumentTypeError("timestamp must include a timezone")
    return parsed


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="sico-etl")
    subparsers = parser.add_subparsers(dest="command", required=True)
    run = subparsers.add_parser("run", help="Extract and synchronize entities")
    run.add_argument(
        "--entity",
        default="all",
        choices=("all", *(entity.value for entity in Entity)),
    )
    run.add_argument("--dry-run", action="store_true", help="Validate without PostgreSQL writes")
    run.add_argument("--updated-since", type=_parse_timestamp)
    return parser


def _client(
    settings: Settings, cancellation: CancellationController
) -> WinBridgeClient:
    return WinBridgeClient(
        base_url=settings.winbridge_base_url,
        timeout_seconds=settings.winbridge_timeout_seconds,
        page_limit=settings.winbridge_page_limit,
        max_retries=settings.winbridge_max_retries,
        retry_base_seconds=settings.winbridge_retry_base_seconds,
        sleeper=cancellation.wait,
    )


def _entities(selected: str) -> tuple[Entity, ...]:
    if selected == "all":
        return ENTITY_ORDER
    return (Entity(selected),)


def execute(args: argparse.Namespace, settings: Settings) -> int:
    entities = _entities(args.entity)
    cancellation = CancellationController()
    cancellation.install_signal_handlers()
    pipeline = EntityPipeline(
        _client(settings, cancellation),
        max_rows=settings.max_rows_per_entity,
        cancellation=cancellation,
    )
    if args.dry_run:
        metrics = [
            pipeline.run(entity, dry_run=True, updated_since=args.updated_since)
            for entity in entities
        ]
        print(metrics_json(metrics))
        return 0
    if not settings.postgres_dsn:
        raise ConfigurationError("POSTGRES_DSN is required outside dry-run mode")

    database = PostgresDatabase(settings.postgres_dsn)
    run = Run()
    metrics = []
    with database.connect() as connection:
        if not database.acquire_lock(connection, settings.lock_id):
            LOGGER.error("Another ETL execution owns advisory lock %s", settings.lock_id)
            return 75
        store = PostgresRunStore(connection)
        store.start(run, "incremental" if args.updated_since else "snapshot")
        try:
            for entity in entities:
                repository = PostgresEntityRepository(
                    connection,
                    CONTRACTS[entity],
                    min_expected_rows=settings.min_expected_rows,
                    max_decrease_percent=settings.max_decrease_percent,
                )
                result = pipeline.run(
                    entity,
                    dry_run=False,
                    repository=repository,
                    updated_since=args.updated_since,
                )
                metrics.append(result)
                store.record_entity(run, result)
            run.finish(RunStatus.SUCCEEDED)
            store.finish(run)
        except Exception as exc:
            run.finish(RunStatus.FAILED, type(exc).__name__)
            store.finish(run)
            raise
        finally:
            database.release_lock(connection, settings.lock_id)
    print(metrics_json(metrics))
    return 0


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        settings = Settings.from_env()
        configure_logging(settings.log_level)
        return execute(args, settings)
    except (ConfigurationError, ContractError) as exc:
        LOGGER.error("Configuration or contract error: %s", exc)
        return 2
    except SourceError as exc:
        LOGGER.error("Source extraction failed: %s", exc)
        return 3
    except CancellationRequested:
        LOGGER.warning("ETL execution cancelled")
        return 130
    except (MappingNotConfirmedError, PublicationSafetyError) as exc:
        LOGGER.error("Publication blocked: %s", exc)
        return 4
    except (PostgresDependencyError, RuntimeError) as exc:
        LOGGER.error("Destination failed: %s", exc)
        return 5
    except Exception as exc:
        LOGGER.error("Unexpected ETL failure: %s", type(exc).__name__)
        return 5
