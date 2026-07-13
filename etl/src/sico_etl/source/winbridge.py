from __future__ import annotations

from datetime import datetime, timezone
import logging
import random
import time
from typing import Any, Callable, Iterator
from urllib.parse import urlencode

from sico_etl.domain.contracts import CONTRACTS, ContractError, EntityContract
from sico_etl.domain.models import Entity, Page
from sico_etl.source.client import (
    HttpTransport,
    InvalidSourceResponse,
    SourceUnavailable,
    UrllibTransport,
    decode_json,
)


LOGGER = logging.getLogger(__name__)
RETRYABLE_STATUS = {429, 502, 503, 504}


def _parse_datetime(value: Any, field: str) -> datetime:
    if not isinstance(value, str):
        raise InvalidSourceResponse(f"{field} must be an ISO 8601 string")
    normalized = value[:-1] + "+00:00" if value.endswith("Z") else value
    try:
        parsed = datetime.fromisoformat(normalized)
    except ValueError as exc:
        raise InvalidSourceResponse(f"{field} must be a valid ISO 8601 timestamp") from exc
    if parsed.tzinfo is None:
        raise InvalidSourceResponse(f"{field} must include a timezone")
    return parsed.astimezone(timezone.utc)


class WinBridgeClient:
    def __init__(
        self,
        base_url: str,
        timeout_seconds: int,
        page_limit: int,
        max_retries: int,
        retry_base_seconds: float,
        transport: HttpTransport | None = None,
        sleeper: Callable[[float], None] = time.sleep,
        jitter: Callable[[], float] = random.random,
    ) -> None:
        self._base_url = base_url.rstrip("/")
        self._timeout_seconds = timeout_seconds
        self._page_limit = page_limit
        self._max_retries = max_retries
        self._retry_base_seconds = retry_base_seconds
        self._transport = transport or UrllibTransport()
        self._sleeper = sleeper
        self._jitter = jitter

    def iter_pages(
        self,
        entity: Entity,
        updated_since: datetime | None = None,
    ) -> Iterator[Page]:
        contract = CONTRACTS[entity]
        if updated_since is not None and not contract.incremental_supported:
            raise ContractError(
                f"{entity.value}: incremental extraction is not confirmed for SICO"
            )
        cursor: str | None = None
        seen_cursors: set[str] = set()
        while True:
            page = self._fetch_page(contract, cursor, updated_since)
            yield page
            if page.next_cursor is None:
                return
            if page.next_cursor in seen_cursors or page.next_cursor == cursor:
                raise InvalidSourceResponse(
                    f"{entity.value}: nextCursor did not advance"
                )
            seen_cursors.add(page.next_cursor)
            cursor = page.next_cursor

    def _fetch_page(
        self,
        contract: EntityContract,
        cursor: str | None,
        updated_since: datetime | None,
    ) -> Page:
        query: dict[str, str | int] = {"limit": self._page_limit}
        if cursor is not None:
            query["cursor"] = cursor
        if updated_since is not None:
            query["updatedSince"] = updated_since.astimezone(timezone.utc).isoformat()
        url = f"{self._base_url}{contract.endpoint}?{urlencode(query)}"
        payload = self._request_with_retries(url)
        raw_items = payload.get("items")
        if not isinstance(raw_items, list):
            raise InvalidSourceResponse("items must be an array")
        if len(raw_items) > self._page_limit:
            raise InvalidSourceResponse("items exceeded the requested page limit")
        next_cursor = payload.get("nextCursor")
        if next_cursor is not None and not isinstance(next_cursor, str):
            raise InvalidSourceResponse("nextCursor must be a string or null")
        extracted_at = _parse_datetime(payload.get("extractedAt"), "extractedAt")
        items: list[dict[str, Any]] = []
        for raw_item in raw_items:
            if not isinstance(raw_item, dict):
                raise InvalidSourceResponse("each item must be an object")
            normalized = dict(raw_item)
            for key in ("sourceCreatedAt", "sourceUpdatedAt"):
                if normalized.get(key) is not None:
                    normalized[key] = _parse_datetime(normalized[key], key)
            items.append(contract.validate(normalized))
        return Page(tuple(items), next_cursor, extracted_at)

    def _request_with_retries(self, url: str) -> dict[str, Any]:
        last_status: int | None = None
        for attempt in range(self._max_retries):
            try:
                response = self._transport.get(url, self._timeout_seconds)
            except SourceUnavailable:
                if attempt + 1 >= self._max_retries:
                    raise SourceUnavailable(
                        "WinBridgeApi remained unreachable after retries"
                    ) from None
                delay = self._retry_base_seconds * (2**attempt) + self._jitter()
                LOGGER.warning(
                    "Retrying unreachable WinBridgeApi (attempt %s/%s)",
                    attempt + 1,
                    self._max_retries,
                )
                self._sleeper(delay)
                continue
            last_status = response.status
            if response.status == 200:
                return decode_json(response.body)
            if response.status not in RETRYABLE_STATUS:
                raise SourceUnavailable(
                    f"WinBridgeApi returned non-retryable HTTP {response.status}"
                )
            if attempt + 1 < self._max_retries:
                delay = self._retry_base_seconds * (2**attempt) + self._jitter()
                LOGGER.warning(
                    "Retrying WinBridgeApi after HTTP %s (attempt %s/%s)",
                    response.status,
                    attempt + 1,
                    self._max_retries,
                )
                self._sleeper(delay)
        raise SourceUnavailable(
            f"WinBridgeApi remained unavailable after retries (HTTP {last_status})"
        )
