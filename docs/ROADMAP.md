# Hoja de ruta y estado

## Estado resumido

**Fase actual:** relevamiento de SICO y validación del ETL en modo seguro.

La API mínima, el túnel y la base del ETL existen. Los seis contratos y mappings
son preliminares; la publicación PostgreSQL permanece bloqueada hasta confirmar
el esquema de SICO y las restricciones del destino.

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
- [ ] Identificar tablas o vistas de artículos, precios y stock.
- [ ] Identificar tablas o vistas de clientes, almacenes y listas de precios.
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
- [ ] Implementar los seis endpoints específicos en WinBridgeApi.
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

Ejecutar `etl/scripts/inspect_postgres.sql` contra PostgreSQL y las consultas de
`discovery/sql/` contra SICO. Completar `docs/ETL_MAPPINGS.md` con evidencia de
claves, relaciones, semántica y volumen. Después implementar un endpoint y un
repositorio piloto, preferentemente de una entidad maestra pequeña, y validarlo de
extremo a extremo antes de habilitar precios o stock.

## Bloqueos actuales

- Falta relevar el esquema y las reglas funcionales de precios y stock del ERP.
- Faltan los objetos SICO y reglas de clientes, almacenes y listas de precios.
- No están confirmadas las claves, constraints ni secuencias PostgreSQL.
- Los endpoints `/api/v1/extract/*` todavía no están implementados.
- El entorno de desarrollo de esta sesión no tiene acceso a los endpoints locales
  del túnel (`127.0.0.1:15000`) ni de WinBridgeApi (`127.0.0.1:5000`).
