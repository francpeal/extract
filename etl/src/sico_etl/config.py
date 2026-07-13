from __future__ import annotations

from dataclasses import dataclass
import os
from urllib.parse import urlparse


class ConfigurationError(ValueError):
    pass


def _positive_int(name: str, default: int) -> int:
    raw = os.getenv(name, str(default))
    try:
        value = int(raw)
    except ValueError as exc:
        raise ConfigurationError(f"{name} must be an integer") from exc
    if value < 1:
        raise ConfigurationError(f"{name} must be greater than zero")
    return value


def _positive_float(name: str, default: float) -> float:
    raw = os.getenv(name, str(default))
    try:
        value = float(raw)
    except ValueError as exc:
        raise ConfigurationError(f"{name} must be numeric") from exc
    if value <= 0:
        raise ConfigurationError(f"{name} must be greater than zero")
    return value


def _percentage(name: str, default: float) -> float:
    raw = os.getenv(name, str(default))
    try:
        value = float(raw)
    except ValueError as exc:
        raise ConfigurationError(f"{name} must be numeric") from exc
    if value < 0 or value > 100:
        raise ConfigurationError(f"{name} must be between 0 and 100")
    return value


@dataclass(frozen=True)
class Settings:
    winbridge_base_url: str
    winbridge_timeout_seconds: int
    winbridge_page_limit: int
    winbridge_max_retries: int
    winbridge_retry_base_seconds: float
    postgres_dsn: str | None
    log_level: str
    lock_id: int
    min_expected_rows: int
    max_decrease_percent: float
    max_rows_per_entity: int

    @classmethod
    def from_env(cls) -> "Settings":
        base_url = os.getenv("WINBRIDGE_BASE_URL", "http://127.0.0.1:15000").rstrip("/")
        parsed_url = urlparse(base_url)
        try:
            parsed_port = parsed_url.port
        except ValueError as exc:
            raise ConfigurationError("WINBRIDGE_BASE_URL has an invalid port") from exc
        if (
            parsed_url.scheme != "http"
            or parsed_url.hostname not in {"127.0.0.1", "localhost"}
            or parsed_port is None
            or parsed_url.username is not None
            or parsed_url.password is not None
            or parsed_url.path not in {"", "/"}
            or parsed_url.query
            or parsed_url.fragment
        ):
            raise ConfigurationError(
                "WINBRIDGE_BASE_URL must target loopback; remote HTTP must remain inside SSH"
            )
        page_limit = _positive_int("WINBRIDGE_PAGE_LIMIT", 500)
        if page_limit > 1000:
            raise ConfigurationError("WINBRIDGE_PAGE_LIMIT cannot exceed 1000")
        retries = _positive_int("WINBRIDGE_MAX_RETRIES", 4)
        max_decrease = _percentage("ETL_MAX_DECREASE_PERCENT", 0.0)
        return cls(
            winbridge_base_url=base_url,
            winbridge_timeout_seconds=_positive_int("WINBRIDGE_TIMEOUT_SECONDS", 30),
            winbridge_page_limit=page_limit,
            winbridge_max_retries=retries,
            winbridge_retry_base_seconds=_positive_float(
                "WINBRIDGE_RETRY_BASE_SECONDS", 0.5
            ),
            postgres_dsn=os.getenv("POSTGRES_DSN"),
            log_level=os.getenv("ETL_LOG_LEVEL", "INFO").upper(),
            lock_id=_positive_int("ETL_LOCK_ID", 726493201),
            min_expected_rows=_positive_int("ETL_MIN_EXPECTED_ROWS", 1),
            max_decrease_percent=max_decrease,
            max_rows_per_entity=_positive_int("ETL_MAX_ROWS_PER_ENTITY", 1000000),
        )
