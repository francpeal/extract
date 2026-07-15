# Matriz de mappings SICO → WinBridgeApi → PostgreSQL

## Estado

Esta matriz define el contrato piloto y registra explícitamente lo que todavía
debe aprobarse. Los orígenes, claves, snapshots completos y restricciones
naturales de PostgreSQL se comprobaron el 2026-07-14. La escritura permanece
bloqueada hasta comparar muestras funcionales y probar los upserts en PostgreSQL
aislado.

Las columnas PostgreSQL proceden de las capturas entregadas. Las claves naturales
están respaldadas por índices únicos observados en la base `dap`. Desde 0.1.5 los
seis mappings están habilitados para publicación; precios y stock se transfieren
sin cálculo ni interpretación adicional.

## Resumen por entidad

| Entidad | Endpoint objetivo | Tabla PostgreSQL | Clave candidata | Estado |
|---|---|---|---|---|
| Artículos | `/api/v1/extract/articles` | `articulos` | `codigo` | 14 284 filas; 15 filas de 6 grupos duplicados excluidas |
| Clientes | `/api/v1/extract/customers` | `clientes` | `cod_dap` | 6 256 filas; RUC vacíos y grupos duplicados excluidos |
| Almacenes | `/api/v1/extract/warehouses` | `almacenes` | `codigo` | 20 filas y unicidad PG confirmada |
| Listas | `/api/v1/extract/price-lists` | `lista_precios` | `codigo` | 13 filas y unicidad PG confirmada |
| Precios | `/api/v1/extract/prices` | `precios` | `cod_articulo, cod_lista` | 18 030 filas; semántica funcional pendiente |
| Stock | `/api/v1/extract/warehouse-stock` | `stock_almacen` | `cod_articulo, cod_almacen` | 14 859 filas; semántica funcional pendiente |

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
| Cliente | `name` | `nombre` | `des_cli`; mismo origen que razón social por consulta confirmada |
| Cliente | `legalName` | `razon_social` | `des_cli`; mismo origen que nombre por consulta confirmada |
| Cliente | `taxId` | `ruc` | `cdg_alt`; vacíos y todos los grupos duplicados se excluyen |
| Cliente | `active` | `estado` | constante verdadera; no representa baja en SICO |
| Cliente | `customerCode` | `cod_dap` | `ruc_cli`; API valida no nulo y único |
| Cliente | `email` | `email` | dato personal; no registrar en logs |
| Cliente | `phone` | `telefono` | dato personal; no registrar en logs |
| Cliente | `mobile` | `celular` | dato personal; no registrar en logs |
| Cliente | `representative` | `representante` | `REP_CLI` |
| Cliente | `assignedSellerCode` | `cod_vendedor_asig` | relación con vendedores pendiente |
| Cliente | `sourceCreatedAt` | `created_at` | `ing_cli` Lima UTC−05:00; nulos usan la fecha centinela fija `2000-01-01 08:00:00` |
| Almacén | `warehouseCode` | `codigo` | `D_TABLAS.NUM_ITEM`, PK, máximo 10 caracteres |
| Almacén | `name` | `nombre` | `LTRIM/RTRIM(DES_ITEM)`; máximo 100 caracteres |
| Almacén | `abbreviation` | `abreviatura` | `LTRIM/RTRIM(ABR_ITEM)`; máximo 10 caracteres |
| Almacén | `active` | `estado` | constante verdadera; no representa baja en SICO |
| Lista | `priceListCode` | `codigo` | `D_TABLAS.NUM_ITEM`, PK, máximo 3 caracteres |
| Lista | `name` | `nombre` | `LTRIM/RTRIM(D_TABLAS.des_item)`; máximo 100 caracteres |
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

1. Objeto y columna SICO confirmados mediante catálogo. **Cumplido.**
2. Clave técnica observada, no inferida por nombre. **Cumplido.**
3. Muestra anonimizada comparada con una pantalla o reporte del ERP. **Diferido,
   no bloquea la proyección actual.**
4. Definición funcional adicional. **No requerida para transferir los campos
   disponibles sin transformación.**
5. Tipo, nulabilidad, unicidad y FK reales en PostgreSQL. **Cumplido.**
6. Volumen, duración y orden de paginación medidos. **Cumplido.**
7. Política explícita para ausencias: conservar, desactivar o eliminar.
   **Cumplido técnicamente:** conservar; falta aceptación funcional de las
   omisiones de origen.

El script `etl/scripts/inspect_postgres.sql` reúne metadatos y duplicados sin
modificar PostgreSQL. El relevamiento de SICO continúa en `docs/ERP_DISCOVERY.md`.

La inspección de `dap` del 2026-07-14 confirmó índices únicos para las seis claves
de sincronización. También confirmó `ix_clientes_ruc`, un índice único adicional
sobre `clientes.ruc`. Como SICO puede contener RUC duplicados por calidad de datos,
esa condición se midió en el origen antes de habilitar clientes.

La medición posterior al `dry-run` encontró 18 grupos de RUC duplicados, con 36
filas y 18 filas excedentes. Cada grupo contiene dos clientes. El responsable
funcional decidió excluir las 36 filas mediante `tax_id_count = 1`; no se elige
una versión arbitraria. El snapshot validado quedó en 6 256 clientes.

La validación del endpoint en Windows del 2026-07-14 midió 6 327 filas en
`m_client`: ninguna tiene `cdg_alt` nulo y 35 lo tienen vacío. El responsable
funcional decidió excluirlas mediante `ISNULL(cdg_alt, '') <> ''`, porque un
cliente sin RUC no puede cotizar ni facturar. El snapshot completo se validó
desde Ubuntu después del redespliegue.

Las seis columnas técnicas `id` usan secuencias propias. La línea base observada
fue: 13 742 artículos, 2 clientes, 20 almacenes, 13 listas, 17 958 precios y
14 830 filas de stock por almacén. Estos conteos son evidencia operativa, no
umbrales definitivos para el origen.

## Consulta confirmada de clientes

El propietario proporcionó la consulta de `m_client` el 2026-07-13. El mapping de
columnas se considera confirmado, con estas salvedades:

- `ruc_cli` es la clave primaria, funciona como cursor y la API comprueba unicidad;
- `ing_cli` representa hora de Lima UTC−05:00 y la API lo convierte a UTC;
- `ing_cli` nulo se sustituye por `2000-01-01 08:00:00` por decisión funcional;
  el valor indica fecha histórica desconocida y permanece estable;
- `updated_at` es siempre nulo en origen;
- `estado` es una constante verdadera, no un indicador observado de SICO;
- `FAX_CLI` alimenta `celular` por definición expresa de la consulta;
- `cdg_alt`/RUC no se usa como clave porque puede contener duplicados;
- las filas que no cumplen `ISNULL(cdg_alt, '') <> ''` no forman parte de la extracción;
- las filas cuyo RUC aparece más de una vez se excluyen todas con
  `tax_id_count = 1`;
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

El snapshot real midió 14 299 filas, sin códigos vacíos ni mayores de 20
caracteres, y 6 grupos de `ArtCod` duplicados. El responsable funcional decidió
omitir por completo esos grupos con `key_count = 1`; no se selecciona la primera
fila ni se deduplica en Python. SICO conserva la responsabilidad de corregirlos.
El snapshot resultante contiene 14 284 filas, por lo que se excluyen 15 filas.

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
