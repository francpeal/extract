# Contrato de integración

## Estado

Este documento separa el contrato actual de diagnóstico del contrato objetivo del
ETL. Los seis endpoints de extracción todavía no están implementados: sus campos
son preliminares hasta confirmar el mapping de SICO según `docs/ETL_MAPPINGS.md`.

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

- `GET /api/v1/extract/articles`
- `GET /api/v1/extract/customers`
- `GET /api/v1/extract/warehouses`
- `GET /api/v1/extract/price-lists`
- `GET /api/v1/extract/prices`
- `GET /api/v1/extract/warehouse-stock`

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
      "commercialDescription": null,
      "category": "01",
      "imageUrl": null,
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

`articleCode` y `active` son obligatorios. La propiedad y origen de `imageUrl`, el
significado de categoría y la regla de activo siguen pendientes.

### Clientes

```json
{
  "items": [
    {
      "name": "Cliente de prueba",
      "legalName": "Cliente de prueba S.A.C.",
      "taxId": "20000000001",
      "active": true,
      "dapCode": "CLI-001",
      "email": "cliente@example.invalid",
      "phone": null,
      "mobile": null,
      "representative": null,
      "assignedSellerCode": "V001",
      "sourceCreatedAt": null,
      "sourceUpdatedAt": null
    }
  ],
  "nextCursor": null,
  "extractedAt": "2026-07-13T12:00:00Z"
}
```

`name`, `legalName`, `taxId` y `active` son obligatorios según el destino actual.
Debe decidirse si `dapCode`, `taxId` u otra combinación identifica al cliente.

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

Código, nombre y estado son obligatorios en el contrato preliminar. Longitudes,
almacenes incluidos y semántica del estado están pendientes.

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
vigencia. El consumidor no puede solicitar listas arbitrarias hasta aprobar esa
regla.

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

Artículo, lista y ambos precios son obligatorios por el esquema PostgreSQL
observado. Moneda, impuestos, vigencia, mínimos, máximos y composición de
descuentos deben validarse funcionalmente antes de implementar la consulta SICO.

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

Artículo y almacén son obligatorios. Deben confirmarse el periodo de acumulación
y si `currentStock` significa físico, disponible u otra cantidad. No se agregan
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
