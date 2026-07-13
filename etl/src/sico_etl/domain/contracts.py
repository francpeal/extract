from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from decimal import Decimal
from typing import Any

from sico_etl.domain.models import Entity


class ContractError(ValueError):
    pass


@dataclass(frozen=True)
class Field:
    name: str
    types: tuple[type, ...]
    required: bool = True
    nullable: bool = False
    max_length: int | None = None


@dataclass(frozen=True)
class EntityContract:
    entity: Entity
    endpoint: str
    destination_table: str
    fields: tuple[Field, ...]
    destination_mapping: dict[str, str]
    natural_key_candidates: tuple[str, ...]
    mapping_confirmed: bool = False
    incremental_supported: bool = False

    def validate(self, item: dict[str, Any]) -> dict[str, Any]:
        if not isinstance(item, dict):
            raise ContractError(f"{self.entity.value}: item must be an object")
        validated: dict[str, Any] = {}
        for field in self.fields:
            if field.name not in item:
                if field.required:
                    raise ContractError(
                        f"{self.entity.value}: missing required field {field.name}"
                    )
                continue
            value = item[field.name]
            if value is None:
                if not field.nullable:
                    raise ContractError(
                        f"{self.entity.value}: {field.name} cannot be null"
                    )
                validated[field.name] = None
                continue
            if bool in field.types and isinstance(value, bool):
                pass
            elif int in field.types and isinstance(value, bool):
                raise ContractError(f"{self.entity.value}: {field.name} has invalid type")
            elif not isinstance(value, field.types):
                expected = ", ".join(t.__name__ for t in field.types)
                raise ContractError(
                    f"{self.entity.value}: {field.name} must be {expected}"
                )
            if field.max_length is not None and isinstance(value, str):
                if len(value) > field.max_length:
                    raise ContractError(
                        f"{self.entity.value}: {field.name} exceeds {field.max_length} characters"
                    )
            if isinstance(value, datetime) and value.tzinfo is None:
                raise ContractError(
                    f"{self.entity.value}: {field.name} must include a timezone"
                )
            if isinstance(value, Decimal) and not value.is_finite():
                raise ContractError(
                    f"{self.entity.value}: {field.name} must be a finite decimal"
                )
            validated[field.name] = value
        return validated


TEXT = (str,)
BOOL = (bool,)
DECIMAL = (Decimal, int)
DATETIME = (datetime,)


def optional(name: str, types: tuple[type, ...], max_length: int | None = None) -> Field:
    return Field(name, types, required=False, nullable=True, max_length=max_length)


CONTRACTS: dict[Entity, EntityContract] = {
    Entity.ARTICLES: EntityContract(
        entity=Entity.ARTICLES,
        endpoint="/api/v1/extract/articles",
        destination_table="articulos",
        fields=(
            Field("articleCode", TEXT, max_length=20),
            optional("description", TEXT),
            Field("active", BOOL),
            optional("brand", TEXT, 120),
            optional("alternateCode", TEXT, 100),
            optional("sourceUpdatedAt", DATETIME),
        ),
        destination_mapping={
            "articleCode": "codigo",
            "description": "descripcion",
            "active": "activo",
            "brand": "marca",
            "alternateCode": "cod_alterno",
        },
        natural_key_candidates=("codigo",),
    ),
    Entity.CUSTOMERS: EntityContract(
        entity=Entity.CUSTOMERS,
        endpoint="/api/v1/extract/customers",
        destination_table="clientes",
        fields=(
            Field("name", TEXT, max_length=150),
            Field("legalName", TEXT, max_length=300),
            Field("taxId", TEXT, max_length=50),
            Field("active", BOOL),
            Field("customerCode", TEXT, max_length=50),
            optional("email", TEXT, 100),
            optional("phone", TEXT, 50),
            optional("mobile", TEXT, 50),
            optional("representative", TEXT, 200),
            optional("assignedSellerCode", TEXT, 10),
            Field("sourceCreatedAt", DATETIME),
            optional("sourceUpdatedAt", DATETIME),
        ),
        destination_mapping={
            "name": "nombre",
            "legalName": "razon_social",
            "taxId": "ruc",
            "active": "estado",
            "customerCode": "cod_dap",
            "email": "email",
            "phone": "telefono",
            "mobile": "celular",
            "representative": "representante",
            "assignedSellerCode": "cod_vendedor_asig",
            "sourceCreatedAt": "created_at",
        },
        natural_key_candidates=("cod_dap",),
    ),
    Entity.WAREHOUSES: EntityContract(
        entity=Entity.WAREHOUSES,
        endpoint="/api/v1/extract/warehouses",
        destination_table="almacenes",
        fields=(
            Field("warehouseCode", TEXT, max_length=10),
            Field("name", TEXT),
            optional("abbreviation", TEXT),
            Field("active", BOOL),
            optional("sourceUpdatedAt", DATETIME),
        ),
        destination_mapping={
            "warehouseCode": "codigo",
            "name": "nombre",
            "abbreviation": "abreviatura",
            "active": "estado",
        },
        natural_key_candidates=("codigo",),
    ),
    Entity.PRICE_LISTS: EntityContract(
        entity=Entity.PRICE_LISTS,
        endpoint="/api/v1/extract/price-lists",
        destination_table="lista_precios",
        fields=(
            Field("priceListCode", TEXT, max_length=3),
            Field("name", TEXT),
            Field("active", BOOL),
            optional("sourceUpdatedAt", DATETIME),
        ),
        destination_mapping={
            "priceListCode": "codigo",
            "name": "nombre",
            "active": "activo",
        },
        natural_key_candidates=("codigo",),
    ),
    Entity.PRICES: EntityContract(
        entity=Entity.PRICES,
        endpoint="/api/v1/extract/prices",
        destination_table="precios",
        fields=(
            Field("articleCode", TEXT, max_length=20),
            Field("priceListCode", TEXT, max_length=3),
            Field("priceUsd", DECIMAL),
            Field("pricePen", DECIMAL),
            optional("minimumUsd", DECIMAL),
            optional("minimumPen", DECIMAL),
            optional("maximumUsd", DECIMAL),
            optional("maximumPen", DECIMAL),
            optional("discount1", DECIMAL),
            optional("discount2", DECIMAL),
            optional("discount3", DECIMAL),
            optional("sourceUpdatedAt", DATETIME),
        ),
        destination_mapping={
            "articleCode": "cod_articulo",
            "priceListCode": "cod_lista",
            "priceUsd": "pre_dol",
            "pricePen": "pre_sol",
            "minimumUsd": "min_dol",
            "minimumPen": "min_sol",
            "maximumUsd": "max_dol",
            "maximumPen": "max_sol",
            "discount1": "por_dct1",
            "discount2": "por_dct2",
            "discount3": "por_dct3",
        },
        natural_key_candidates=("cod_articulo", "cod_lista"),
    ),
    Entity.WAREHOUSE_STOCK: EntityContract(
        entity=Entity.WAREHOUSE_STOCK,
        endpoint="/api/v1/extract/warehouse-stock",
        destination_table="stock_almacen",
        fields=(
            Field("articleCode", TEXT, max_length=20),
            Field("warehouseCode", TEXT, max_length=10),
            optional("openingStock", DECIMAL),
            optional("incomingStock", DECIMAL),
            optional("outgoingStock", DECIMAL),
            optional("currentStock", DECIMAL),
            optional("sourceUpdatedAt", DATETIME),
        ),
        destination_mapping={
            "articleCode": "cod_articulo",
            "warehouseCode": "cod_almacen",
            "openingStock": "stock_inicial",
            "incomingStock": "stock_ingresos",
            "outgoingStock": "stock_salidas",
            "currentStock": "stock_actual",
        },
        natural_key_candidates=("cod_articulo", "cod_almacen"),
    ),
}
