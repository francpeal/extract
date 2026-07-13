# Contrato de integración

## Estado

Este documento separa el contrato existente de la propuesta objetivo. Los nombres
y campos definitivos dependen del relevamiento del esquema y las reglas del ERP.

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
recibido del cliente y no forman parte del contrato productivo objetivo.

`POST /query` acepta en `params` valores escalares JSON (`string`, `number`,
`boolean` o `null`). Los objetos y arreglos son rechazados con HTTP 400.

Ambos endpoints `/query` tienen un timeout HTTP de 30 segundos y un timeout SQL
de 25 segundos. Un timeout responde HTTP 504.

## Contrato objetivo preliminar

La forma final se decidirá después de conocer volumen, frecuencia y posibilidades
de extracción incremental. Como referencia:

### `GET /api/v1/articles/prices`

Parámetros candidatos:

- `updatedSince`: cursor temporal, si el ERP dispone de uno confiable.
- `cursor` y `limit`: paginación estable.
- `priceList`: solo si el consumidor puede elegir entre listas permitidas.

Campos mínimos candidatos:

```json
{
  "items": [
    {
      "articleCode": "ART-001",
      "priceList": "GENERAL",
      "currency": "PEN",
      "amount": 10.50,
      "sourceUpdatedAt": null
    }
  ],
  "nextCursor": null,
  "extractedAt": "2026-07-13T12:00:00Z"
}
```

### `GET /api/v1/articles/stock`

Parámetros candidatos:

- `updatedSince`: si existe un cursor confiable.
- `cursor` y `limit`: paginación estable.
- `warehouse`: filtro opcional dentro de valores permitidos.

Campos mínimos candidatos:

```json
{
  "items": [
    {
      "articleCode": "ART-001",
      "warehouseCode": "MAIN",
      "onHand": 12.0,
      "available": 10.0,
      "committed": 2.0,
      "sourceUpdatedAt": null
    }
  ],
  "nextCursor": null,
  "extractedAt": "2026-07-13T12:00:00Z"
}
```

Los campos `available`, `committed`, moneda y fechas son hipótesis de contrato; no
deben implementarse hasta confirmar su semántica en el ERP.

## Convenciones propuestas

- Fechas en UTC y formato ISO 8601.
- Importes y cantidades como números decimales, nunca texto formateado.
- Códigos conservados como texto para no perder ceros iniciales.
- Respuesta paginada y orden determinista cuando el volumen lo requiera.
- `400` para parámetros inválidos, `500` para fallo interno, `503` para dependencia
  no disponible y `504` para timeout.
- El consumidor debe poder reintentar lecturas sin efectos secundarios.

## Antes de estabilizar v1

- Confirmar consultas y reglas con datos reales del ERP.
- Medir número de artículos, almacenes y tiempo de consulta.
- Definir extracción completa o incremental.
- Definir qué errores son reintentables.
- Acordar el máximo de página y el orden estable.
