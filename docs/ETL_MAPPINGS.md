# Matriz de mappings SICO → WinBridgeApi → PostgreSQL

## Estado

Esta matriz define el límite de contrato objetivo y registra explícitamente lo
que todavía debe confirmarse. Ya se documentaron los orígenes y claves de las seis
entidades. La escritura del ETL permanece bloqueada en código hasta comprobar las
restricciones naturales de PostgreSQL y validar muestras y semántica funcional.

Las columnas PostgreSQL proceden de las capturas entregadas. Las claves naturales
son candidatas, no restricciones confirmadas.

## Resumen por entidad

| Entidad | Endpoint objetivo | Tabla PostgreSQL | Clave candidata | Estado |
|---|---|---|---|---|
| Artículos | `/api/v1/extract/articles` | `articulos` | `codigo` | origen confirmado; constraint PG pendiente |
| Clientes | `/api/v1/extract/customers` | `clientes` | `cod_dap` | origen y fecha confirmados; constraint PG pendiente |
| Almacenes | `/api/v1/extract/warehouses` | `almacenes` | `codigo` | origen confirmado; constraint PG pendiente |
| Listas | `/api/v1/extract/price-lists` | `lista_precios` | `codigo` | origen confirmado; constraint PG pendiente |
| Precios | `/api/v1/extract/prices` | `precios` | `cod_articulo, cod_lista` | origen y clave compuesta confirmados; constraint PG pendiente |
| Stock | `/api/v1/extract/warehouse-stock` | `stock_almacen` | `cod_articulo, cod_almacen` | origen y clave compuesta confirmados; semántica y constraint PG pendientes |

## Campos preliminares

| Entidad | Campo API | Destino PostgreSQL | Estado / observación |
|---|---|---|---|
| Artículo | `articleCode` | `codigo` | `VW_Articulo.ArtCod`, clave primaria |
| Artículo | `description` | `descripcion` | `LTRIM/RTRIM(ArtNombre)` |
| Artículo | — | `descripcion_comercial` | propiedad de la web; ETL no actualiza |
| Artículo | — | `categoria` | propiedad de la web; ETL no actualiza |
| Artículo | — | `imagen_url` | propiedad de la web; ETL no actualiza |
| Artículo | `active` | `activo` | constante verdadera; no representa baja en SICO |
| Artículo | `brand` | `marca` | substring de `ArtLineaDesc` después de `-` |
| Artículo | `alternateCode` | `cod_alterno` | `ArtCodInt`; no es clave de sincronización |
| Cliente | `name` | `nombre` | diferencia con razón social pendiente |
| Cliente | `legalName` | `razon_social` | origen pendiente |
| Cliente | `taxId` | `ruc` | `cdg_alt`; duplicados aceptados por calidad del origen |
| Cliente | `active` | `estado` | regla funcional pendiente |
| Cliente | `customerCode` | `cod_dap` | `ruc_cli`; API valida no nulo y único |
| Cliente | `email` | `email` | dato personal; no registrar en logs |
| Cliente | `phone` | `telefono` | dato personal; no registrar en logs |
| Cliente | `mobile` | `celular` | dato personal; no registrar en logs |
| Cliente | `representative` | `representante` | origen pendiente |
| Cliente | `assignedSellerCode` | `cod_vendedor_asig` | relación con vendedores pendiente |
| Cliente | `sourceCreatedAt` | `created_at` | `ing_cli` Lima UTC−05:00, normalizado a UTC |
| Almacén | `warehouseCode` | `codigo` | `D_TABLAS.NUM_ITEM`, PK, máximo 10 caracteres |
| Almacén | `name` | `nombre` | `LTRIM/RTRIM(DES_ITEM)` |
| Almacén | `abbreviation` | `abreviatura` | `LTRIM/RTRIM(ABR_ITEM)` |
| Almacén | `active` | `estado` | constante verdadera; no representa baja en SICO |
| Lista | `priceListCode` | `codigo` | `D_TABLAS.NUM_ITEM`, PK, máximo 3 caracteres |
| Lista | `name` | `nombre` | `LTRIM/RTRIM(D_TABLAS.des_item)` |
| Lista | `active` | `activo` | constante verdadera; no representa baja en SICO |
| Precio | `articleCode` | `cod_articulo` | `M_PRECIO.CDG_PROD`; primera parte de la PK |
| Precio | `priceListCode` | `cod_lista` | `M_PRECIO.CDG_LPRC`; segunda parte de la PK |
| Precio | `priceUsd` | `pre_dol` | `M_PRECIO.PRE_DOL`; interpretación funcional pendiente |
| Precio | `pricePen` | `pre_sol` | `M_PRECIO.PRE_SOL`; interpretación funcional pendiente |
| Precio | `minimumUsd` | `min_dol` | `M_PRECIO.min_dol`; se transfiere sin cálculo |
| Precio | `minimumPen` | `min_sol` | `M_PRECIO.min_sol`; se transfiere sin cálculo |
| Precio | `maximumUsd` | `max_dol` | `M_PRECIO.MAX_DOL`; se transfiere sin cálculo |
| Precio | `maximumPen` | `max_sol` | `M_PRECIO.MAX_SOL`; se transfiere sin cálculo |
| Precio | `discount1..3` | `por_dct1..3` | `M_PRECIO.POR_DCT1..3`; composición pendiente |
| Stock | `articleCode` | `cod_articulo` | `M_STOCK.CDG_PROD`; primera parte de la PK |
| Stock | `warehouseCode` | `cod_almacen` | `M_STOCK.CDG_AREA`; segunda parte de la PK |
| Stock | `openingStock` | `stock_inicial` | `M_STOCK.STK_INIC`; periodo y regla pendientes |
| Stock | `incomingStock` | `stock_ingresos` | `M_STOCK.STK_ING`; movimientos incluidos pendientes |
| Stock | `outgoingStock` | `stock_salidas` | `M_STOCK.STK_SAL`; movimientos incluidos pendientes |
| Stock | `currentStock` | `stock_actual` | `M_STOCK.STK_ACT`; físico/disponible/comprometido pendiente |

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

## Consulta confirmada de clientes

El propietario proporcionó la consulta de `m_client` el 2026-07-13. El mapping de
columnas se considera confirmado, con estas salvedades:

- `ruc_cli` es la clave primaria, funciona como cursor y la API comprueba unicidad;
- `ing_cli` representa hora de Lima UTC−05:00 y la API lo convierte a UTC;
- `updated_at` es siempre nulo en origen;
- `estado` es una constante verdadera, no un indicador observado de SICO;
- `FAX_CLI` alimenta `celular` por definición expresa de la consulta;
- `cdg_alt`/RUC no se usa como clave porque puede contener duplicados;
- una ausencia en el snapshot no desactiva ni elimina al cliente.

La consulta efectiva vive en
`WinBridgeApi/Extraction/CustomerExtraction.cs`; no se duplica aquí para evitar
que documentación y código diverjan.

## Consulta confirmada de artículos

La consulta de `VW_Articulo` fue proporcionada el 2026-07-13. `ArtCod` es la clave
primaria y se usa como cursor. La API valida código no nulo, único y de hasta 20
caracteres. La carga es un snapshot completo con `activo=true` y fecha de origen
nula. `descripcion_comercial`, `categoria` e `imagen_url` se excluyen del contrato
y del mapping de escritura para preservar los valores gestionados por la web. La
consulta efectiva vive en `WinBridgeApi/Extraction/ArticleExtraction.cs`.

## Consulta confirmada de listas de precios

La consulta de `D_TABLAS` filtrada por `CDG_TAB = 'PRC'` fue proporcionada el
2026-07-13. `NUM_ITEM` es la clave primaria y se utiliza como cursor. La API
comprueba que sea no nulo, único y de hasta tres caracteres. `activo` es una
constante verdadera y `updated_at` es nulo, por lo que la carga es un snapshot
completo conservador. La consulta efectiva vive en
`WinBridgeApi/Extraction/PriceListExtraction.cs`.

## Consulta confirmada de almacenes

La consulta de `D_TABLAS` filtrada por `CDG_TAB = 'ARE'` fue proporcionada el
2026-07-13. `NUM_ITEM` es la clave primaria y el cursor. La API comprueba código no
nulo, único y de hasta 10 caracteres. `estado` es siempre verdadero y
`updated_at` nulo, de modo que la extracción es un snapshot completo conservador.
La consulta efectiva vive en `WinBridgeApi/Extraction/WarehouseExtraction.cs`.

## Consulta confirmada de precios

La consulta de `M_PRECIO` fue proporcionada el 2026-07-13. La clave primaria es
el par `(CDG_PROD, CDG_LPRC)` y se utiliza completo como cursor; ninguna de sus
partes se considera única por separado. La API valida ambos códigos, la unicidad
del par y los importes obligatorios `PRE_DOL` y `PRE_SOL`. Mínimos, máximos y
descuentos se exponen como decimales opcionales, sin aplicar fórmulas ni inferir
su semántica. `updated_at` es nulo, por lo que la carga es un snapshot completo
con paginación keyset. La consulta efectiva vive en
`WinBridgeApi/Extraction/PriceExtraction.cs`.

## Consulta confirmada de stock por almacén

La consulta de `M_STOCK` fue proporcionada el 2026-07-13. La clave primaria es el
par `(CDG_PROD, CDG_AREA)` y se utiliza completo como cursor; ninguna parte se
considera única por separado. La API valida ambos códigos y la unicidad del par.
`STK_INIC`, `STK_ING`, `STK_SAL` y `STK_ACT` se exponen como decimales opcionales,
sin aplicar fórmulas ni inferir su semántica. `updated_at` es nulo, por lo que la
carga es un snapshot completo con paginación keyset. La consulta efectiva vive en
`WinBridgeApi/Extraction/WarehouseStockExtraction.cs`.
