from __future__ import annotations

from dataclasses import asdict
from datetime import datetime
import json
from typing import Any
from uuid import UUID

from sico_etl.domain.models import EntityMetrics


def _json_default(value: Any) -> str:
    if isinstance(value, (datetime, UUID)):
        return value.isoformat() if isinstance(value, datetime) else str(value)
    if hasattr(value, "value"):
        return str(value.value)
    raise TypeError(f"Cannot serialize {type(value).__name__}")


def metrics_json(metrics: list[EntityMetrics]) -> str:
    return json.dumps(
        [asdict(item) for item in metrics],
        default=_json_default,
        ensure_ascii=False,
        separators=(",", ":"),
    )
