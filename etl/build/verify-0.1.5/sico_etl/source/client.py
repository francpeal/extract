from __future__ import annotations

from dataclasses import dataclass
import json
from decimal import Decimal
from typing import Any, Protocol
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


class SourceError(RuntimeError):
    pass


class SourceUnavailable(SourceError):
    pass


class InvalidSourceResponse(SourceError):
    pass


@dataclass(frozen=True)
class TransportResponse:
    status: int
    body: bytes


class HttpTransport(Protocol):
    def get(self, url: str, timeout: int) -> TransportResponse: ...


class UrllibTransport:
    MAX_RESPONSE_BYTES = 10 * 1024 * 1024

    def get(self, url: str, timeout: int) -> TransportResponse:
        request = Request(url, headers={"Accept": "application/json"}, method="GET")
        try:
            with urlopen(request, timeout=timeout) as response:  # noqa: S310 - loopback URL enforced
                body = response.read(self.MAX_RESPONSE_BYTES + 1)
                if len(body) > self.MAX_RESPONSE_BYTES:
                    raise InvalidSourceResponse("WinBridgeApi response exceeded 10 MiB")
                return TransportResponse(status=response.status, body=body)
        except HTTPError as exc:
            return TransportResponse(
                status=exc.code,
                body=exc.read(self.MAX_RESPONSE_BYTES + 1)[: self.MAX_RESPONSE_BYTES],
            )
        except (URLError, TimeoutError, OSError) as exc:
            raise SourceUnavailable("WinBridgeApi is unreachable") from exc


def decode_json(body: bytes) -> dict[str, Any]:
    try:
        value = json.loads(body.decode("utf-8"), parse_float=Decimal)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise InvalidSourceResponse("WinBridgeApi returned invalid JSON") from exc
    if not isinstance(value, dict):
        raise InvalidSourceResponse("WinBridgeApi response must be a JSON object")
    return value
