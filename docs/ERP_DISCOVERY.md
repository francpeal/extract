# Relevamiento de precios y stock del ERP

## Objetivo y estado

Este procedimiento reúne evidencia para identificar los objetos, claves y reglas
que representan artículos, precios y stock. El contrato piloto técnico ya existe;
este documento queda como guía para cerrar su aceptación funcional.

- **Confirmado:** SQL Server es 2012 SP1 y se accede mediante WinBridgeApi.
- **Confirmado:** el flujo Ubuntu → SSH → API → SQL Server funcionó manualmente.
- **Confirmado:** `M_PRECIO` y `M_STOCK`, sus columnas de extracción y sus claves
  compuestas.
- **Confirmado:** snapshots completos de las seis entidades, 53 462 filas en 111
  páginas y aproximadamente 114 segundos.
- **Confirmado:** no existe una marca confiable para extracción incremental; el
  piloto usa snapshots completos paginados.
- **Pendiente:** semántica funcional de precios, moneda, impuestos, stock,
  almacenes incluidos y aprobación de omisiones por calidad del origen.
- **Ruta operativa:** las consultas reales se ejecutan desde el Ubuntu que posee
  el túnel o desde Windows contra la API local; el entorno de desarrollo no
  necesita acceder a esos loopbacks.

Los archivos de `discovery/sql/` son consultas de catálogo o lectura compatibles
con SQL Server 2012. Los términos de búsqueda de `02_candidate_columns.sql` son
heurísticos: señalan candidatos, no confirman reglas de negocio.

## Controles antes de ejecutar

1. Confirmar con `GET /health` que se usa el endpoint esperado.
2. Confirmar la base efectiva y la identidad SQL con la consulta 01.
3. Usar una cuenta SQL de solo lectura. Si aún no está confirmada, registrar el
   resultado como riesgo y no interpretar el acceso actual como permiso objetivo.
4. No guardar credenciales ni resultados con datos reales en Git.
5. Ejecutar una consulta por vez y conservar duración, cantidad de filas y error.

## Ejecución mediante `/query`

Ejemplo en PowerShell desde Windows. Cambiar `$baseUrl` a
`http://127.0.0.1:15000` si se ejecuta donde está activo el túnel:

```powershell
$baseUrl = 'http://127.0.0.1:5000'
$sql = Get-Content -Raw '.\discovery\sql\01_connection_context.sql'
$body = @{ sql = $sql; params = @{} } | ConvertTo-Json -Depth 4
Invoke-RestMethod -Method Post -Uri "$baseUrl/query" `
  -ContentType 'application/json' -Body $body
```

Para una consulta parametrizada:

```powershell
$sql = Get-Content -Raw '.\discovery\sql\03_object_columns.sql'
$body = @{
  sql = $sql
  params = @{ schemaName = 'ESQUEMA_CONFIRMADO'; objectName = 'OBJETO_CONFIRMADO' }
} | ConvertTo-Json -Depth 4
Invoke-RestMethod -Method Post -Uri "$baseUrl/query" `
  -ContentType 'application/json' -Body $body
```

## Orden de relevamiento

1. `01_connection_context.sql`: registrar base, login, usuario, versión y permisos
   visibles. Si la base no es la esperada, detener el relevamiento.
2. `02_candidate_columns.sql`: inventariar objetos candidatos y su tamaño
   aproximado. Ausencia de resultados puede significar nombres no contemplados o
   falta de `VIEW DEFINITION`; no demuestra que los datos no existan.
3. Para cada candidato razonable, ejecutar `03_object_columns.sql` y
   `04_object_keys_and_relations.sql`. Priorizar vistas soportadas por el ERP si
   existe evidencia de que son su interfaz de consulta.
4. Solo después de revisar columnas, ejecutar `05_sample_object.sql` con 10 filas.
   Puede devolver datos comerciales: no registrar el resultado completo en logs
   ni incorporarlo al repositorio.
5. Contrastar una muestra con las pantallas o reportes del ERP y con un responsable
   funcional. Una coincidencia técnica aislada no confirma la regla.

## Evidencia mínima a registrar fuera de datos sensibles

| Tema | Evidencia requerida |
|---|---|
| Artículo | objeto, columna de código, unicidad, estado activo/inactivo |
| Precio | objeto, relación con artículo, lista/tipo, vigencia, moneda, impuestos |
| Stock | objeto, relación con artículo, cantidad y definición funcional |
| Almacén | objeto/código, granularidad y almacenes incluidos |
| Volumen | filas aproximadas y duración de consultas representativas |
| Incremental | columna candidata, zona horaria, estabilidad y prueba de cambios |

## Preguntas que requieren confirmación funcional

- ¿Aprueba el consumidor `VW_Articulo.ArtCod` como identificador ya implementado?
- ¿Qué lista o tipo de precio debe extraerse y el importe incluye impuestos?
- ¿Cuál es la moneda y puede variar por lista o artículo?
- ¿Stock significa físico, disponible, comprometido u otra cantidad?
- ¿Se requiere total o desglose por almacén/sucursal y cuáles se incluyen?
- ¿Cómo se representan artículos inactivos, precios vencidos y cantidades nulas?
- ¿Qué frecuencia, volumen y latencia necesita el consumidor?

## Criterio de cierre del relevamiento

Las consultas, claves, volúmenes y recorrido técnico ya están confirmados. El
cierre restante exige muestras comparadas con el ERP y definiciones funcionales;
hasta entonces, los endpoints y DTO de `docs/API.md` conservan estado piloto.
