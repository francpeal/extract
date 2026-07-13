# Hoja de ruta y estado

## Estado resumido

**Fase actual:** relevamiento de SICO y validación del ETL en modo seguro.

La API mínima, el túnel y la base del ETL existen. Las seis consultas, contratos y
claves de origen están implementados como pilotos; la publicación PostgreSQL
permanece bloqueada hasta validar la semántica funcional, las muestras y las
restricciones del destino.

## Completado

- [x] Elegir la arquitectura API local más túnel SSH.
- [x] Crear proyecto ASP.NET Core 8 con acceso mediante `Microsoft.Data.SqlClient`.
- [x] Crear endpoint de salud.
- [x] Crear endpoint temporal para probar consultas SQL.
- [x] Incorporar timeout, cancelación y logging básico.
- [x] Preparar ejecución interactiva y registro como servicio Windows.
- [x] Documentar contexto, arquitectura y método de continuidad.
- [x] Integrar el ciclo de vida nativo de Windows Services.
- [x] Restringir Kestrel a loopback.
- [x] Convertir parámetros JSON escalares a tipos aceptados por SqlClient.
- [x] Aplicar un timeout HTTP de 30 segundos a los endpoints SQL.
- [x] Desplegar WinBridgeApi como servicio automático en Windows Server 2012.
- [x] Validar desde Windows la conexión a SQL Server 2012 SP1 (11.0.3156.0 x64).
- [x] Validar manualmente el flujo Ubuntu → SSH → WinBridgeApi → SQL Server.
- [x] Ejecutar el túnel SSH como servicio `systemd` habilitado al arranque.
- [x] Documentar operación, migración Linux, rollback y diagnóstico.
- [x] Crear la base Python del ETL con CLI y configuración por entorno.
- [x] Incorporar paginación, reintentos, DTO, decimales y timestamps con zona.
- [x] Incorporar advisory lock, publicación transaccional y guardas de mapping.
- [x] Crear migraciones de control y plantillas systemd sin desplegarlas.
- [x] Crear fixtures y pruebas aisladas sin acceso al ERP.

## Fase 1 — Descubrimiento del ERP

- [x] Confirmar versión y edición de SQL Server.
- [x] Preparar consultas de catálogo y muestreo limitado para el relevamiento.
- [ ] Confirmar edición y nivel de parches exactos de Windows Server 2012.
- [ ] Confirmar cadena de conexión efectiva y usuario de solo lectura.
- [ ] Inspeccionar constraints, índices, secuencias y duplicados de las seis tablas PostgreSQL.
- [x] Identificar `M_PRECIO` como origen de precios.
- [x] Identificar `M_STOCK` como origen de stock por almacén.
- [x] Identificar `VW_Articulo` como origen de artículos.
- [x] Identificar `D_TABLAS` con `CDG_TAB = 'ARE'` como origen de almacenes.
- [x] Identificar `D_TABLAS` con `CDG_TAB = 'PRC'` como origen de listas de precios.
- [x] Identificar `m_client` como origen de clientes.
- [x] Confirmar la consulta y mapping de columnas de clientes desde `m_client`.
- [x] Confirmar `ruc_cli` como clave primaria y `ing_cli` como hora Lima UTC−05:00.
- [ ] Confirmar claves y relaciones mediante consultas de muestra.
- [ ] Acordar semántica de precio, moneda, impuestos, stock y almacén.
- [ ] Medir volumen de datos y duración de las consultas.
- [ ] Determinar si existe una marca confiable para extracción incremental.

## Fase 2 — Contrato v1

- [x] Definir DTO preliminares de las seis entidades con fixtures anonimizados.
- [x] Documentar la matriz SICO → API → PostgreSQL y sus pendientes.
- [ ] Elegir snapshot completo, paginación, cursor incremental o combinación.
- [ ] Definir validaciones, códigos de error y límites.
- [ ] Aprobar el contrato con el consumidor Ubuntu.

## Fase 3 — Implementación productiva

- [ ] Separar configuración, acceso SQL, modelos y endpoints.
- [ ] Implementar consultas conocidas y parametrizadas.
- [ ] Incorporar límites y paginación/cursor acordados.
- [x] Enlazar Kestrel a localhost.
- [ ] Eliminar los endpoints de SQL arbitrario.
- [ ] Evitar exposición de detalles internos en errores.
- [x] Añadir pruebas unitarias del ETL sin el ERP real.
- [x] Crear cliente ETL para los seis endpoints objetivo.
- [x] Implementar `dry-run` y validación sin escrituras.
- [x] Bloquear publicación de mappings no confirmados.
- [x] Implementar endpoint paginado de clientes con validación de clave de origen.
- [x] Implementar endpoint paginado de listas con validación de `NUM_ITEM`.
- [x] Implementar endpoint paginado de artículos preservando campos de la web.
- [x] Implementar endpoint paginado de almacenes con validación de `NUM_ITEM`.
- [x] Implementar endpoint paginado de precios con clave compuesta.
- [x] Implementar endpoint paginado de stock con clave compuesta.
- [ ] Confirmar mappings y habilitar repositorios entidad por entidad.
- [ ] Probar migraciones y upserts contra PostgreSQL aislado.
- [ ] Implementar staging persistente para snapshots grandes si el volumen lo exige.

## Fase 4 — Despliegue y aceptación

- [x] Publicar en modo Release y desplegar con configuración externa.
- [ ] Ejecutar con una cuenta de servicio de privilegio mínimo.
- [x] Validar acceso por túnel desde Ubuntu.
- [ ] Validar que el puerto no es accesible por otras interfaces.
- [ ] Probar timeout, caída de SQL Server, reinicio del servicio y recuperación.
- [ ] Comparar muestras de precios y stock contra el ERP.
- [ ] Documentar operación, rollback y responsables.

## Próximo paso recomendado

Validar los seis endpoints implementados contra SICO y ejecutar
`etl/scripts/inspect_postgres.sql` para comprobar la restricción única de
`clientes.cod_dap`, `lista_precios.codigo`, `articulos.codigo` y
`almacenes.codigo`, además de la restricción compuesta de
`precios(cod_articulo, cod_lista)` y
`stock_almacen(cod_articulo, cod_almacen)`, y detectar nulos/duplicados antes de
habilitar los upserts. Mantener las seis publicaciones bloqueadas y comparar
muestras anonimizadas con el ERP.

## Bloqueos actuales

- Falta validar funcionalmente moneda, impuestos, vigencia y descuentos de
  precios, además del significado y periodo de las cantidades de stock.
- No están confirmadas las claves, constraints ni secuencias PostgreSQL.
- Los seis endpoints existen como pilotos y requieren validación contra SICO.
- Falta confirmar o crear controladamente la unicidad de `clientes.cod_dap`.
- Falta confirmar o crear controladamente la unicidad de `lista_precios.codigo`.
- Falta confirmar o crear controladamente la unicidad de `articulos.codigo`.
- Falta confirmar o crear controladamente la unicidad de `almacenes.codigo`.
- Falta confirmar o crear controladamente la unicidad compuesta de
  `precios(cod_articulo, cod_lista)`.
- Falta confirmar o crear controladamente la unicidad compuesta de
  `stock_almacen(cod_articulo, cod_almacen)`.
- El entorno de desarrollo de esta sesión no tiene acceso a los endpoints locales
  del túnel (`127.0.0.1:15000`) ni de WinBridgeApi (`127.0.0.1:5000`).
