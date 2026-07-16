# Contrato de integración

## Estado

Este documento separa el contrato temporal de diagnóstico del contrato piloto del
ETL. Los seis endpoints específicos están desplegados y recorrieron snapshots
completos con datos reales el 2026-07-14. La publicación PostgreSQL está
habilitada en `sico-etl` 0.1.5 para las seis entidades; los valores se proyectan
sin recalcularlos ni añadir semántica fuera del contrato expuesto.

La operación productiva del 2026-07-15 confirmó una sincronización completa
exitosa por el mismo contrato: 53 469 filas en 111 páginas, sin rechazos. Los
conteos por recurso pueden variar entre snapshots; los listados de validación que
siguen corresponden a la medición técnica del 2026-07-14.

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

El ETL nunca consume `/query`. Debe retirarse o protegerse explícitamente antes
de cerrar la aceptación de seguridad definitiva. Su excepción temporal durante la
operación productiva controlada se documenta en `docs/SECURITY.md` y ADR-013.

## Convenciones del contrato piloto desplegado

Endpoints:

- `GET /api/v1/extract/articles` — validado: 14 284 filas, 29 páginas
- `GET /api/v1/extract/customers` — validado: 6 256 filas, 13 páginas
- `GET /api/v1/extract/warehouses` — validado: 20 filas, 1 página
- `GET /api/v1/extract/price-lists` — validado: 13 filas, 1 página
- `GET /api/v1/extract/prices` — validado: 18 030 filas, 37 páginas
- `GET /api/v1/extract/warehouse-stock` — validado: 14 859 filas, 30 páginas

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

La validación real encontró 6 grupos con `ArtCod` duplicado. Por decisión del
responsable funcional, WinBridgeApi excluye todas las filas pertenecientes a esos
grupos mediante `key_count = 1`; no elige arbitrariamente una versión. Corregir la
duplicidad es responsabilidad del propietario de SICO. La ausencia resultante no
desactiva ni elimina artículos locales. El snapshot validado contiene 14 284
artículos y excluye 15 filas pertenecientes a los 6 grupos duplicados.

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
de permitir la paginación. PostgreSQL tiene una restricción única equivalente
sobre `cod_dap`, confirmada el 2026-07-14.

La consulta proporcionada define:

- `ruc_cli → customerCode → cod_dap`;
- `des_cli → name` y `legalName`;
- `cdg_alt → taxId → ruc`;
- las filas con `ISNULL(cdg_alt, '') = ''` se excluyen porque el negocio requiere RUC para
  cotizar o facturar;
- `CONVERT(bit, 1) → active`, por lo que todos los clientes extraídos están activos;
- `EMA_CLI`, `TEL_CLI`, `FAX_CLI`, `REP_CLI` y `CDG_VEND` para los campos de contacto;
- `sourceUpdatedAt = null` porque SICO no expone fecha de modificación.

`ing_cli` se interpreta como hora de Lima (UTC−05:00), según confirmación del
propietario, y se normaliza a UTC en `sourceCreatedAt`. `ruc_cli` es la clave
primaria del origen. `cdg_alt`/RUC no participa en el cursor ni en el upsert: puede
contener duplicados por problemas de calidad, aunque funcionalmente debería ser
único.

Cuando `ing_cli` es nulo, la consulta usa la fecha centinela fija
`2000-01-01 08:00:00` mediante `DATETIMEFROMPARTS(2000,1,1,8,0,0,0)`, validado
contra SQL Server 2012. Ese valor indica que la fecha histórica de SICO es
desconocida y permanece estable entre snapshots.

La medición del 2026-07-14 encontró 35 de 6 327 filas de `m_client` con `cdg_alt`
vacío y ninguna con valor nulo. El responsable funcional decidió excluirlas con
`ISNULL(cdg_alt, '') <> ''`, porque sin RUC el cliente no puede cotizar ni
facturar. El filtro desplegado recorrió correctamente el snapshot completo desde
Ubuntu.

Entre los 6 292 clientes restantes se encontraron 18 grupos de RUC duplicado, con
36 filas. Por decisión del responsable funcional, la consulta exige
`tax_id_count = 1` y excluye todas las filas de esos grupos; no elige un cliente
arbitrario. El snapshot validado después del filtro contiene 6 256 clientes.

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
no se interpretan ni se derivan: los campos disponibles se transfieren sin
cálculo. Cualquier ampliación de esa semántica requiere un cambio de alcance.

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
por la clave compuesta. No se deriva una cantidad disponible ni comprometida: los
cuatro campos se transfieren tal como SICO los entrega. Cualquier interpretación
adicional queda fuera del alcance actual.

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

Ya están confirmados con datos reales las consultas, los snapshots completos
paginados, el orden estable, los volúmenes, las restricciones y secuencias de
PostgreSQL y la política técnica conservadora de no desactivar por ausencia.

Mejoras posteriores no bloqueantes:

- pruebas de upsert e idempotencia en PostgreSQL aislado;
- retirar o proteger `/query` antes de una aceptación de seguridad definitiva;
- ampliar, si se solicita, la semántica comercial de precios o stock.
