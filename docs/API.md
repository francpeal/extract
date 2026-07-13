# Contrato de integración

## Estado

Este documento separa el contrato actual de diagnóstico del contrato objetivo del
ETL. Los seis endpoints específicos están implementados como pilotos y pendientes
de validación con datos reales. La publicación PostgreSQL permanece bloqueada
según `docs/ETL_MAPPINGS.md`.

## Contrato actual de diagnóstico

### `GET /health`

Indica que el proceso HTTP está activo. No garantiza acceso a SQL Server.

Respuesta `200`:

```json
{
  "status": "ok",
  "timestamp": "2026-07-13T12:00:00.0000000Z"
}
```

### `GET /query` y `POST /query`

Endpoints temporales para validar conectividad y explorar el esquema. Ejecutan SQL
recibido del cliente y no forman parte del contrato productivo.

`POST /query` acepta en `params` valores escalares JSON (`string`, `number`,
`boolean` o `null`). Objetos y arreglos se rechazan con HTTP 400. Ambos endpoints
tienen timeout HTTP de 30 segundos y SQL de 25 segundos. Un timeout responde 504.

El ETL nunca consume `/query`. Debe retirarse antes de producción después de que
los endpoints específicos y el relevamiento estén completos.

## Convenciones del contrato objetivo

Endpoints:

- `GET /api/v1/extract/articles` — implementado, pendiente de validación real
- `GET /api/v1/extract/customers` — implementado, pendiente de validación real
- `GET /api/v1/extract/warehouses` — implementado, pendiente de validación real
- `GET /api/v1/extract/price-lists` — implementado, pendiente de validación real
- `GET /api/v1/extract/prices` — implementado, pendiente de validación real
- `GET /api/v1/extract/warehouse-stock` — implementado, pendiente de validación real

Parámetros comunes:

| Parámetro | Regla |
|---|---|
| `limit` | entero de 1 a 1000; valor operativo inicial 500 |
| `cursor` | opaco; emitido por la página anterior; no debe interpretarlo el cliente |
| `updatedSince` | ISO 8601 con zona; rechazado mientras la entidad no tenga cursor confiable |

Toda página responde:

```json
{
  "items": [],
  "nextCursor": null,
  "extractedAt": "2026-07-13T12:00:00Z"
}
```

Reglas:

- orden estable y determinista respaldado por una clave confirmada;
- códigos como texto, preservando ceros iniciales;
- fechas ISO 8601 con zona, normalizadas a UTC;
- importes y cantidades como números decimales JSON, no texto formateado;
- `nextCursor` nulo solo al finalizar el dataset;
- una página nunca contiene más filas que `limit`;
- lecturas idempotentes, sin efectos secundarios;
- nombres internos de SICO no aparecen en el contrato ni en errores;
- `sourceUpdatedAt` es opcional hasta confirmar una marca confiable en SICO.

## DTO preliminares

### Artículos

```json
{
  "items": [
    {
      "articleCode": "ART-001",
      "description": "Artículo de prueba",
      "active": true,
      "brand": "MARCA",
      "alternateCode": "ALT-001",
      "sourceUpdatedAt": null
    }
  ],
  "nextCursor": null,
  "extractedAt": "2026-07-13T12:00:00Z"
}
```

`articleCode` y `active` son obligatorios. La consulta confirmada usa
`VW_Articulo.ArtCod` como clave primaria, `ArtNombre` como descripción,
`ArtLineaDesc` para derivar marca y `ArtCodInt` como código alterno. `active` es
siempre verdadero y `sourceUpdatedAt` siempre nulo.

`commercialDescription`, `category` e `imageUrl` no forman parte del DTO: son
propiedad de la aplicación web y el ETL no debe insertarlos ni actualizarlos. Una
ausencia en el snapshot tampoco desactiva automáticamente el artículo.

### Clientes

```json
{
  "items": [
    {
      "name": "Cliente de prueba",
      "legalName": "Cliente de prueba S.A.C.",
      "taxId": "20000000001",
      "active": true,
      "customerCode": "CLI-001",
      "email": "cliente@example.invalid",
      "phone": null,
      "mobile": null,
      "representative": null,
      "assignedSellerCode": "V001",
      "sourceCreatedAt": "2026-07-13T17:00:00Z",
      "sourceUpdatedAt": null
    }
  ],
  "nextCursor": null,
  "extractedAt": "2026-07-13T12:00:00Z"
}
```

`customerCode`, `name`, `legalName`, `taxId` y `active` son obligatorios. El
endpoint valida que `ruc_cli`, origen de `customerCode`, sea no nulo y único antes
de permitir la paginación. Falta confirmar que PostgreSQL tenga una restricción
única equivalente sobre `cod_dap`.

La consulta proporcionada define:

- `ruc_cli → customerCode → cod_dap`;
- `des_cli → name` y `legalName`;
- `cdg_alt → taxId → ruc`;
- `CONVERT(bit, 1) → active`, por lo que todos los clientes extraídos están activos;
- `EMA_CLI`, `TEL_CLI`, `FAX_CLI`, `REP_CLI` y `CDG_VEND` para los campos de contacto;
- `sourceUpdatedAt = null` porque SICO no expone fecha de modificación.

`ing_cli` se interpreta como hora de Lima (UTC−05:00), según confirmación del
propietario, y se normaliza a UTC en `sourceCreatedAt`. `ruc_cli` es la clave
primaria del origen. `cdg_alt`/RUC no participa en el cursor ni en el upsert: puede
contener duplicados por problemas de calidad, aunque funcionalmente debería ser
único.

### Almacenes

```json
{
  "items": [
    {
      "warehouseCode": "001",
      "name": "ALMACÉN DE PRUEBA",
      "abbreviation": "ALM1",
      "active": true,
      "sourceUpdatedAt": null
    }
  ],
  "nextCursor": null,
  "extractedAt": "2026-07-13T12:00:00Z"
}
```

Código, nombre y estado son obligatorios. La consulta confirmada usa `D_TABLAS`
con `CDG_TAB = 'ARE'`, toma `NUM_ITEM` como clave primaria, `DES_ITEM` como nombre
y `ABR_ITEM` como abreviatura. `active` es siempre verdadero y
`sourceUpdatedAt` siempre nulo. La ausencia en un snapshot no desactiva el almacén.
El código se limita a 10 caracteres para ser compatible con
`stock_almacen.cod_almacen`.

### Listas de precios

```json
{
  "items": [
    {
      "priceListCode": "001",
      "name": "LISTA DE PRUEBA",
      "active": true,
      "sourceUpdatedAt": null
    }
  ],
  "nextCursor": null,
  "extractedAt": "2026-07-13T12:00:00Z"
}
```

Debe confirmarse qué listas consume la aplicación y cómo se representa su
vigencia. La consulta confirmada usa `D_TABLAS` con `CDG_TAB = 'PRC'`, toma
`NUM_ITEM` como clave primaria y `des_item` como nombre. `active` es siempre
verdadero y `sourceUpdatedAt` siempre nulo; la ausencia de una lista en un snapshot
no provoca desactivación automática. El código se limita a tres caracteres para
ser compatible con `precios.cod_lista`.

### Precios

```json
{
  "items": [
    {
      "articleCode": "ART-001",
      "priceListCode": "001",
      "priceUsd": 12.34,
      "pricePen": 45.67,
      "minimumUsd": null,
      "minimumPen": null,
      "maximumUsd": null,
      "maximumPen": null,
      "discount1": 0.00,
      "discount2": null,
      "discount3": null,
      "sourceUpdatedAt": null
    }
  ],
  "nextCursor": null,
  "extractedAt": "2026-07-13T12:00:00Z"
}
```

La consulta confirmada usa `M_PRECIO` y la clave primaria compuesta
`(CDG_PROD, CDG_LPRC)`, expuesta como `(articleCode, priceListCode)`. El endpoint
valida que el par sea no nulo, único y compatible con los límites de 20 y 3
caracteres del destino. Artículo, lista, `PRE_DOL` y `PRE_SOL` son obligatorios;
los mínimos, máximos y descuentos admiten nulo. Los valores se transfieren sin
recalcularlos ni interpretar reglas comerciales.

Como el origen no aporta fecha de modificación, `sourceUpdatedAt` es nulo y el
endpoint rechaza `updatedSince`: la extracción es un snapshot completo paginado
por la clave compuesta. Moneda, impuestos, vigencia y composición de descuentos
aún deben validarse funcionalmente antes de habilitar la publicación.

### Stock por almacén

```json
{
  "items": [
    {
      "articleCode": "ART-001",
      "warehouseCode": "001",
      "openingStock": 10.00,
      "incomingStock": 2.00,
      "outgoingStock": 1.00,
      "currentStock": 11.00,
      "sourceUpdatedAt": null
    }
  ],
  "nextCursor": null,
  "extractedAt": "2026-07-13T12:00:00Z"
}
```

La consulta confirmada usa `M_STOCK` y la clave primaria compuesta
`(CDG_PROD, CDG_AREA)`, expuesta como `(articleCode, warehouseCode)`. El endpoint
valida que el par sea no nulo, único y compatible con los límites de 20 y 10
caracteres del destino. `STK_INIC`, `STK_ING`, `STK_SAL` y `STK_ACT` admiten nulo
y se transfieren sin recalcularlos.

Como el origen no aporta fecha de modificación, `sourceUpdatedAt` es nulo y el
endpoint rechaza `updatedSince`: la extracción es un snapshot completo paginado
por la clave compuesta. Aún deben confirmarse el periodo de acumulación y si
`STK_ACT` representa stock físico, disponible u otra cantidad. No se agregan
campos `available` o `committed` sin evidencia.

## Errores

Formato objetivo:

```json
{
  "error": {
    "code": "dependency_unavailable",
    "message": "The data source is temporarily unavailable.",
    "retryable": true,
    "requestId": "00-00000000000000000000000000000000-0000000000000000-00"
  }
}
```

| HTTP | Uso | Reintentable |
|---:|---|---|
| 400 | parámetros, cursor o límite inválidos | no |
| 500 | fallo interno no clasificado | no por defecto |
| 503 | SQL Server o dependencia no disponible | sí |
| 504 | timeout de consulta o solicitud | sí |

Los errores no contienen SQL, tabla, credenciales, cadena de conexión ni datos de
filas.

## Antes de estabilizar v1

- Confirmar consultas y mappings con datos reales de SICO.
- Confirmar constraints, secuencias, FKs y duplicados en PostgreSQL.
- Medir volumen, duración y orden estable de cada entidad.
- Definir snapshot completo o incremental por entidad.
- Aprobar política de ausencias y desactivación.
- Comparar muestras anonimizadas con el ERP y obtener aprobación funcional.
