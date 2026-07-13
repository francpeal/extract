# Matriz de mappings SICO → WinBridgeApi → PostgreSQL

## Estado

Esta matriz define el límite de contrato objetivo y registra explícitamente lo
que todavía debe confirmarse. Ninguna columna interna de SICO está documentada;
por ello, todos los mappings de origen permanecen pendientes y la escritura del
ETL está bloqueada en código.

Las columnas PostgreSQL proceden de las capturas entregadas. Las claves naturales
son candidatas, no restricciones confirmadas.

## Resumen por entidad

| Entidad | Endpoint objetivo | Tabla PostgreSQL | Clave candidata | Estado |
|---|---|---|---|---|
| Artículos | `/api/v1/extract/articles` | `articulos` | `codigo` | pendiente de SICO y constraints PG |
| Clientes | `/api/v1/extract/customers` | `clientes` | `cod_dap` o `ruc` | ambigua; bloquea publicación |
| Almacenes | `/api/v1/extract/warehouses` | `almacenes` | `codigo` | tipos y constraint PG pendientes |
| Listas | `/api/v1/extract/price-lists` | `lista_precios` | `codigo` | tipos y constraint PG pendientes |
| Precios | `/api/v1/extract/prices` | `precios` | `cod_articulo, cod_lista` | semántica y constraint pendientes |
| Stock | `/api/v1/extract/warehouse-stock` | `stock_almacen` | `cod_articulo, cod_almacen` | semántica y constraint pendientes |

## Campos preliminares

| Entidad | Campo API | Destino PostgreSQL | Estado / observación |
|---|---|---|---|
| Artículo | `articleCode` | `codigo` | columna PG confirmada; origen SICO pendiente |
| Artículo | `description` | `descripcion` | columna PG confirmada; origen pendiente |
| Artículo | `commercialDescription` | `descripcion_comercial` | origen pendiente |
| Artículo | `category` | `categoria` | significado del código pendiente |
| Artículo | `imageUrl` | `imagen_url` | no confirmar que SICO sea el propietario del dato |
| Artículo | `active` | `activo` | regla de activo/inactivo pendiente |
| Artículo | `brand` | `marca` | origen pendiente |
| Artículo | `alternateCode` | `cod_alterno` | unicidad y significado pendientes |
| Cliente | `name` | `nombre` | diferencia con razón social pendiente |
| Cliente | `legalName` | `razon_social` | origen pendiente |
| Cliente | `taxId` | `ruc` | formato y unicidad pendientes |
| Cliente | `active` | `estado` | regla funcional pendiente |
| Cliente | `dapCode` | `cod_dap` | candidato a clave; confirmar obligatoriedad |
| Cliente | `email` | `email` | dato personal; no registrar en logs |
| Cliente | `phone` | `telefono` | dato personal; no registrar en logs |
| Cliente | `mobile` | `celular` | dato personal; no registrar en logs |
| Cliente | `representative` | `representante` | origen pendiente |
| Cliente | `assignedSellerCode` | `cod_vendedor_asig` | relación con vendedores pendiente |
| Cliente | `sourceCreatedAt` | `created_at` | mapping o default local pendiente; bloquea inserts |
| Almacén | `warehouseCode` | `codigo` | tipo/longitud PG y origen pendientes |
| Almacén | `name` | `nombre` | origen pendiente |
| Almacén | `abbreviation` | `abreviatura` | origen pendiente |
| Almacén | `active` | `estado` | almacenes incluidos pendiente |
| Lista | `priceListCode` | `codigo` | tipo/longitud y origen pendientes |
| Lista | `name` | `nombre` | origen pendiente |
| Lista | `active` | `activo` | regla de vigencia pendiente |
| Precio | `articleCode` | `cod_articulo` | relación con artículos pendiente de FK |
| Precio | `priceListCode` | `cod_lista` | relación con listas pendiente de FK |
| Precio | `priceUsd` | `pre_dol` | confirmar moneda, impuestos y vigencia |
| Precio | `pricePen` | `pre_sol` | confirmar moneda, impuestos y vigencia |
| Precio | `minimumUsd` | `min_dol` | semántica pendiente |
| Precio | `minimumPen` | `min_sol` | semántica pendiente |
| Precio | `maximumUsd` | `max_dol` | semántica pendiente |
| Precio | `maximumPen` | `max_sol` | semántica pendiente |
| Precio | `discount1..3` | `por_dct1..3` | orden y composición pendientes |
| Stock | `articleCode` | `cod_articulo` | relación con artículos pendiente de FK |
| Stock | `warehouseCode` | `cod_almacen` | relación con almacenes pendiente de FK |
| Stock | `openingStock` | `stock_inicial` | periodo y regla pendientes |
| Stock | `incomingStock` | `stock_ingresos` | periodo y movimientos incluidos pendientes |
| Stock | `outgoingStock` | `stock_salidas` | periodo y movimientos incluidos pendientes |
| Stock | `currentStock` | `stock_actual` | físico/disponible/comprometido pendiente |

`sourceUpdatedAt` aparece en todos los DTO como opcional. Solo podrá poblarse y
habilitar `updatedSince` si SICO tiene una marca estable, con zona horaria y prueba
de cambios. `extractedAt` pertenece al envelope y lo genera WinBridgeApi.

## Evidencia requerida para desbloquear cada mapping

1. Objeto y columna SICO confirmados mediante catálogo.
2. Clave y relaciones observadas, no inferidas por nombre.
3. Muestra anonimizada comparada con una pantalla o reporte del ERP.
4. Definición aprobada por un responsable funcional.
5. Tipo, nulabilidad, unicidad y FK reales en PostgreSQL.
6. Volumen, duración y orden de paginación medidos.
7. Política explícita para ausencias: conservar, desactivar o eliminar.

El script `etl/scripts/inspect_postgres.sql` reúne metadatos y duplicados sin
modificar PostgreSQL. El relevamiento de SICO continúa en `docs/ERP_DISCOVERY.md`.
