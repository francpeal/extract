# Hoja de ruta y estado

## Estado resumido

**Fase actual:** operación productiva y monitoreo.

La API, el túnel y el ETL están desplegados. Las seis consultas, contratos y
claves de origen se publican a PostgreSQL desde `sico-etl` 0.1.5 mediante su
servicio `oneshot` registrado e invocado por un timer `systemd` habilitado. Los valores de precios y stock se
transfieren sin cálculos ni interpretación adicional; los temas funcionales y de
seguridad restantes no bloquean la operación actual.

**Evidencia operativa del 2026-07-15:** snapshots programados exitosos cada cinco
minutos. La última ejecución observada recorrió 53 469 filas en 111 páginas en
aproximadamente 153 segundos: 20 almacenes, 13 listas, 14 289 artículos, 6 256
clientes, 18 032 precios y 14 859 stocks. No hubo rechazos, inserciones ni
actualizaciones porque PostgreSQL ya coincidía con el snapshot.

## Punto exacto de reanudación

No repetir, salvo que cambien código, consultas, binarios o configuración:

- despliegue y registro del servicio Windows `WinBridgeApi`;
- instalación inicial de `sico-etl` 0.1.4, posterior actualización operativa a
  0.1.5, migración de las cinco tablas de control, creación del rol `sico_etl` y
  configuración de `PGPASSFILE`;
- inspección de constraints, índices, secuencias, tipos y nulabilidad de las seis
  tablas PostgreSQL;
- validación individual y por túnel de los seis endpoints;
- mediciones y filtros de calidad de artículos y clientes;
- `--entity all --dry-run`: 53 462 filas, 111 páginas, aproximadamente 114
  segundos y 0 rechazos, sin escrituras.
- publicación productiva programada de `sico-etl` 0.1.5, verificada el
  2026-07-15 con 53 469 filas, 111 páginas y 0 rechazos.

Operación actual:

1. ejecutar `validate_ubuntu.sh` como comprobación de solo lectura;
2. revisar `journalctl -u sico-etl.service` y la última sincronización registrada;
3. investigar cualquier fallo antes de cambiar frecuencia o controles.

Artefactos del cierre de validación inicial (2026-07-14):

- WinBridgeApi:
  `D:\Desarrollo\extract\WinBridgeApi\publish\WinBridgeApi-customer-duplicate-filter-dll-20260714-154408.zip`,
  SHA-256 `d12ed5822c739841cf683490baa38d2b09de5cb0b1d5ce7b2ecc1313b87c3590`;
  DLL desplegada SHA-256
  `969b19c994ed964ed3996aaac24e5e6623375268df74b5430fb11fceefe399db`;
- ETL:
  `D:\Desarrollo\extract\WinBridgeApi\publish\sico-etl-0.1.4-pilot-20260714-140438.zip`,
  SHA-256
  `5f3a79afecb5da2a178dd74bb727071047ef8c9dbbe15dcf0020b621b346000a`;
  wheel SHA-256
  `1d99e413412dc3daf70fa4090e9b4e06cfc40a9126ece5e28d6f50de55608a97`.

El paquete y SHA-256 efectivos de `sico-etl` 0.1.5 no quedaron registrados en
esta sesión. La versión instalada se comprobó en producción el 2026-07-15; antes
de una actualización o rollback se deben recuperar y registrar el ZIP, wheel y
sus sumas sin incluir secretos.

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
- [x] Preparar paquete Release piloto de WinBridgeApi sin configuración productiva.
- [x] Preparar paquete piloto instalable del ETL Linux sin secretos.
- [x] Alinear el ETL con Python 3.10 nativo de Ubuntu 22.04.
- [x] Instalar el piloto `sico-etl` en `/opt/sico-etl` con usuario y `venv` dedicados.
- [x] Validar en Ubuntu 22.04/Python 3.10 las 30 pruebas (29 OK, 1 integración omitida).
- [x] Confirmar PostgreSQL 17.10 local y existencia de la base `dap` en Ubuntu.
- [x] Aplicar en `dap` la migración transaccional de cinco tablas de control ETL.
- [x] Crear la cuenta PostgreSQL `sico_etl` con privilegios mínimos sobre once tablas.
- [x] Configurar autenticación local mediante `PGPASSFILE`, sin contraseña en el DSN.
- [x] Documentar el despliegue reproducible del ETL en Ubuntu.
- [x] Actualizar WinBridgeApi en Windows con la versión que expone los seis endpoints.
- [x] Validar en Windows una página real de artículos, almacenes, listas, precios y stock.
- [x] Medir clientes en SICO: 6 327 filas totales y 35 sin `cdg_alt`/RUC.
- [x] Definir que 35 clientes con `cdg_alt` vacío se excluyen por no ser aptos para cotizar o facturar.
- [x] Desplegar `ISNULL(cdg_alt, '') <> ''` y validar una página real de clientes en Windows.
- [x] Validar por el túnel HTTP 200 en los seis endpoints desde Ubuntu.
- [x] Identificar incompatibilidad de Python 3.10 con timestamps .NET de siete dígitos.
- [x] Preparar corrección ETL para normalizar timestamps .NET a microsegundos.
- [x] Medir artículos: 14 299 filas, 6 grupos de `ArtCod` duplicados y ningún código vacío o mayor de 20.
- [x] Definir que todos los grupos de artículos duplicados se omiten de la extracción.
- [x] Definir `2000-01-01 08:00:00` como fecha centinela para clientes con `ing_cli` nulo.
- [x] Instalar y probar `sico-etl` 0.1.4 en Ubuntu/Python 3.10 (30 OK, 1 omitida).
- [x] Completar `--entity all --dry-run` final: 53 462 filas, 114 segundos y 0 rechazos.
- [x] Medir RUC duplicados: 18 grupos, 36 filas y 18 filas excedentes.
- [x] Definir que se excluyen las 36 filas pertenecientes a grupos de RUC duplicado.
- [x] Antes del despliegue, verificar que `sico-etl.service` y `sico-etl.timer` no estaban registrados en Ubuntu.
- [x] Crear scripts de diagnóstico de solo lectura para el servicio Windows y el entorno Ubuntu.
- [x] Instalar `sico-etl` 0.1.5 y habilitar la publicación productiva de las seis entidades.
- [x] Observar ejecuciones programadas exitosas del ETL y registrar evidencia operativa.

## Fase 1 — Descubrimiento del ERP

- [x] Confirmar versión y edición de SQL Server.
- [x] Preparar consultas de catálogo y muestreo limitado para el relevamiento.
- [ ] Confirmar edición y nivel de parches exactos de Windows Server 2012.
- [ ] Confirmar cadena de conexión efectiva y usuario de solo lectura.
- [x] Inspeccionar constraints e índices de las seis tablas PostgreSQL.
- [x] Confirmar secuencias de `id` y volúmenes base de las seis tablas PostgreSQL.
- [x] Inspeccionar tipos, longitudes y nulabilidad de las seis tablas PostgreSQL.
- [x] Medir calidad técnica del origen y los snapshots completos.
- [ ] Validar muestras funcionales con el responsable del ERP.
- [x] Identificar `M_PRECIO` como origen de precios.
- [x] Identificar `M_STOCK` como origen de stock por almacén.
- [x] Identificar `VW_Articulo` como origen de artículos.
- [x] Identificar `D_TABLAS` con `CDG_TAB = 'ARE'` como origen de almacenes.
- [x] Identificar `D_TABLAS` con `CDG_TAB = 'PRC'` como origen de listas de precios.
- [x] Identificar `m_client` como origen de clientes.
- [x] Confirmar la consulta y mapping de columnas de clientes desde `m_client`.
- [x] Confirmar `ruc_cli` como clave primaria y `ing_cli` como hora Lima UTC−05:00.
- [x] Confirmar claves técnicas mediante consultas y snapshots completos.
- [ ] Confirmar relaciones y significado comercial mediante muestras funcionales.
- [ ] Acordar semántica de precio, moneda, impuestos, stock y almacén.
- [x] Medir volumen de datos y duración del recorrido integrado.
- [x] Determinar que no existe una marca confiable para extracción incremental.

## Fase 2 — Contrato v1

- [x] Definir DTO preliminares de las seis entidades con fixtures anonimizados.
- [x] Documentar la matriz SICO → API → PostgreSQL y sus pendientes.
- [x] Elegir snapshots completos con paginación por clave estable.
- [x] Definir validaciones, códigos de error y límites del piloto.
- [ ] Aprobar el contrato con el consumidor Ubuntu.

## Fase 3 — Implementación productiva

- [x] Separar configuración, acceso SQL, modelos y endpoints.
- [x] Implementar consultas conocidas y parametrizadas.
- [x] Incorporar límites y paginación por clave estable.
- [x] Enlazar Kestrel a localhost.
- [ ] Retirar o proteger los endpoints de SQL arbitrario después del piloto.
- [x] Evitar exposición de detalles internos en los errores de los endpoints de
  extracción.
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
- [x] Confirmar mappings y habilitar repositorios entidad por entidad.
- [ ] Probar migraciones y upserts contra PostgreSQL aislado.
- [ ] Implementar staging persistente para snapshots grandes si el volumen lo exige.

## Fase 4 — Despliegue y aceptación

- [x] Publicar en modo Release y desplegar con configuración externa.
- [x] Actualizar el servicio Windows con la versión que contiene los seis endpoints.
- [x] Configurar y registrar el servicio ETL y habilitar su timer.
- [x] Ejecutar con la cuenta de servicio `sico-etl` de privilegio mínimo.
- [x] Validar acceso por túnel desde Ubuntu.
- [ ] Validar que el puerto no es accesible por otras interfaces.
- [ ] Probar timeout, caída de SQL Server, reinicio del servicio y recuperación.
- [ ] Comparar muestras de precios y stock contra el ERP.
- [x] Documentar operación y rollback.
- [ ] Confirmar responsables operativos y funcionales.

## Próximo paso recomendado

Ejecutar periódicamente `validate_ubuntu.sh` y revisar la evidencia del ETL. En
paralelo, recuperar y registrar el artefacto y SHA-256 efectivos de 0.1.5, y
revisar la exposición de PostgreSQL mediante `listen_addresses`, `pg_hba.conf` y
firewall. La validación funcional de muestras de precios y stock continúa como
mejora de aceptación, no como bloqueo de la operación. No volver a ejecutar
`inspect_postgres.sql` sobre `dap`: las claves, índices, secuencias, tipos y
nulabilidad ya fueron confirmados.

## Bloqueos actuales

- Falta validar funcionalmente moneda, impuestos, vigencia y descuentos de
  precios, además del significado y periodo de las cantidades de stock.
- Las claves, índices únicos, secuencias, tipos y nulabilidad PostgreSQL están
  confirmados; falta una prueba de upsert en PostgreSQL aislado como mejora de
  cobertura, aunque la publicación productiva ya fue observada exitosamente.
- Los seis endpoints y la publicación programada están validados técnicamente.
  Falta aprobación funcional de la semántica comercial.
- Clientes extrae 6 256 filas después de excluir 35 RUC vacíos y 36 filas de 18
  grupos duplicados; usa `2000-01-01 08:00:00` como centinela estable para
  `ing_cli` nulo.
- La medición inicial de artículos extrajo 14 284 filas después de excluir 15
  filas pertenecientes a 6 grupos de `ArtCod` duplicados; el snapshot productivo
  observado el 2026-07-15 tuvo 14 289 filas. Los conteos de origen pueden variar
  entre snapshots.
- Los filtros de calidad evitan RUC vacíos o duplicados. Falta aprobar
  funcionalmente las omisiones, pero la entidad ya está habilitada y operativa.
- Falta registrar el paquete y las sumas SHA-256 efectivas de `sico-etl` 0.1.5
  para hacer reproducibles una actualización o rollback.
- El entorno de desarrollo de esta sesión no tiene acceso a los endpoints locales
  del túnel (`127.0.0.1:15000`) ni de WinBridgeApi (`127.0.0.1:5000`).
- PostgreSQL escucha actualmente en todas las interfaces IPv4 e IPv6; falta
  verificar firewall y `pg_hba.conf` antes de cerrar la aceptación productiva.
