# Registro de decisiones de arquitectura

Las decisiones se agregan de forma cronológica. No se borran: si una decisión
cambia, se marca como sustituida y se referencia la nueva.

## ADR-001 — Puente HTTP junto al ERP

- **Estado:** aceptada
- **Fecha:** 2026-07-13
- **Contexto:** La aplicación Ubuntu necesita consultar SQL Server 2012, pero la
  conexión directa presentó problemas de compatibilidad y no se puede modificar
  la infraestructura de base de datos del cliente.
- **Decisión:** Ejecutar una API .NET en Windows que acceda localmente a SQL Server
  y exponga datos mediante HTTP a través de un túnel SSH.
- **Consecuencias:** Se desacopla Ubuntu del protocolo SQL antiguo. Se incorpora un
  componente que debe desplegarse y operarse en el entorno del cliente.

## ADR-002 — SSH como límite principal de acceso

- **Estado:** aceptada para el alcance inicial
- **Fecha:** 2026-07-13
- **Contexto:** El túnel acepta como origen únicamente la IP del servidor Ubuntu y
  la API se proyecta para exposición local.
- **Decisión:** No exigir autenticación adicional en la API durante el primer hito.
  Restringir la escucha a localhost, mantener el control por IP y limitar la API a
  lecturas específicas.
- **Consecuencias:** Menor complejidad operativa. Si cambia la topología o aumenta
  la superficie de acceso, se debe reabrir esta decisión.

## ADR-003 — API específica, no SQL remoto genérico

- **Estado:** aceptada
- **Fecha:** 2026-07-13
- **Contexto:** `/query` permitió demostrar conectividad, pero entrega al consumidor
  control total sobre el SQL autorizado por la cuenta de base de datos.
- **Decisión:** Mantener `/query` solo durante exploración controlada y reemplazarlo
  por endpoints versionados de precios y stock antes de producción.
- **Consecuencias:** La API deberá mapear el esquema ERP a DTO propios, pero el
  contrato será estable, auditable y de solo lectura.

## ADR-004 — El consumidor orquesta la extracción

- **Estado:** sustituida por ADR-007
- **Fecha:** 2026-07-13
- **Contexto:** El servidor Ubuntu ya aloja la aplicación consumidora.
- **Decisión propuesta:** La programación, persistencia, reintentos de alto nivel y
  cursores pertenecen a Ubuntu; WinBridgeApi permanece sin estado.
- **Consecuencias:** La API Windows se mantiene pequeña. Requiere que el contrato
  permita paginación o cursores reproducibles.

## ADR-005 — Escucha exclusiva en loopback

- **Estado:** aceptada
- **Fecha:** 2026-07-13
- **Contexto:** El requisito inicial indicaba `0.0.0.0`, pero la arquitectura real
  utiliza un túnel cuyo destino está en el mismo servidor Windows. Escuchar en
  todas las interfaces ampliaba innecesariamente la superficie de red.
- **Decisión:** Kestrel escucha únicamente en localhost y el túnel reenvía hacia
  `127.0.0.1:5000`.
- **Consecuencias:** Los equipos de la LAN no pueden invocar directamente la API.
  No se debe abrir el puerto 5000 en el firewall.

## ADR-006 — Hosting nativo como servicio Windows

- **Estado:** aceptada
- **Fecha:** 2026-07-13
- **Contexto:** Registrar un proceso con `sc.exe` no configura por sí mismo el
  ciclo de vida requerido por el Service Control Manager.
- **Decisión:** Integrar `Microsoft.Extensions.Hosting.WindowsServices` y
  `AddWindowsService`, conservando la ejecución interactiva para diagnóstico.
- **Consecuencias:** El mismo build publicado funciona como servicio o en consola
  y resuelve la configuración desde el directorio publicado.

## ADR-007 — PostgreSQL como proyección local orquestada desde Linux

- **Estado:** aceptada
- **Fecha:** 2026-07-13
- **Contexto:** La aplicación comercial prioriza agilidad y no debe acoplar cada
  lectura a la disponibilidad y latencia de SICO, Windows y el túnel.
- **Decisión:** Un ETL independiente en Linux consume WinBridgeApi y sincroniza las
  seis entidades pertinentes hacia PostgreSQL. SICO permanece como fuente de
  verdad y WinBridgeApi permanece sin estado.
- **Consecuencias:** La aplicación lee localmente y tolera caídas temporales del
  origen. Se incorpora operación, monitoreo y almacenamiento de una proyección.

## ADR-008 — Publicación idempotente con mappings protegidos

- **Estado:** aceptada
- **Fecha:** 2026-07-13
- **Contexto:** Las capturas de PostgreSQL sugieren claves naturales, pero no
  prueban constraints ni identifican las claves y reglas internas de SICO.
- **Decisión:** Mantener todos los mappings con `mapping_confirmed=false` y
  bloquear escrituras hasta completar el relevamiento. Una vez confirmados, el
  ETL preservará IDs locales y hará upsert transaccional por clave natural.
- **Consecuencias:** Es posible desarrollar y probar extracción y validación sin
  arriesgar datos. La primera carga productiva requiere una aprobación explícita.

## ADR-009 — Snapshot conservador sin marca de modificación

- **Estado:** aceptada para la primera puesta en marcha
- **Fecha:** 2026-07-13
- **Contexto:** El propietario de las consultas no identifica una marca estable de
  modificación en las tablas SICO; las consultas confirmadas de las seis entidades
  devuelven `updated_at` nulo.
- **Decisión:** Preparar snapshots completos paginados con límites y controles de volumen.
  `updatedSince` permanece deshabilitado por entidad hasta demostrar un cursor
  confiable. La ausencia de una fila no provoca borrado ni desactivación.
- **Consecuencias:** Se favorece consistencia y recuperación a costa de releer más
  datos. La estrategia podrá evolucionar por entidad con mediciones reales.

## ADR-010 — ETL Python independiente y operado por systemd

- **Estado:** aceptada; requisito de versión sustituido por ADR-014
- **Fecha:** 2026-07-13
- **Contexto:** La orquestación, PostgreSQL y el túnel residen en Linux; no deben
  incorporarse al ciclo de vida de la aplicación web ni de WinBridgeApi.
- **Decisión:** Implementar `etl/` como paquete Python 3.11+ con CLI, configuración
  por entorno y plantillas `systemd` de tipo oneshot/timer.
- **Consecuencias:** Despliegue y rollback independientes. Python y psycopg pasan a
  ser dependencias operativas del sincronizador Linux.

## ADR-011 — Normalización de fechas SICO desde hora Lima

- **Estado:** aceptada
- **Fecha:** 2026-07-13
- **Contexto:** SQL Server y Linux operan en GMT−5, hora de Lima. `m_client.ing_cli`
  no incluye desplazamiento, pero representa esa hora local.
- **Decisión:** Interpretar `ing_cli` con offset UTC−05:00 en WinBridgeApi y emitir
  `sourceCreatedAt` normalizado a UTC. No usar la zona configurada dinámicamente
  en el proceso para evitar resultados distintos entre servidores.
- **Consecuencias:** PostgreSQL recibe instantes inequívocos. Si la semántica del
  dato histórico fuera distinta, deberá sustituirse esta decisión y reprocesarse.

## ADR-012 — Propiedad local de campos enriquecidos de artículos

- **Estado:** aceptada
- **Fecha:** 2026-07-13
- **Contexto:** La aplicación web administra `descripcion_comercial`, `categoria`
  e `imagen_url`; SICO no debe sobrescribir ese enriquecimiento local.
- **Decisión:** Excluir esos campos del DTO de extracción y del mapping de escritura
  del ETL. SICO es propietario solamente de código, descripción, activo, marca y
  código alterno dentro de la tabla `articulos`.
- **Consecuencias:** Los upserts preservan los campos locales incluso en snapshots
  completos. Cambiar su propiedad requerirá un nuevo contrato y una ADR.

## ADR-013 — Excepción temporal para conservar `/query` en el piloto

- **Estado:** aceptada temporalmente
- **Fecha:** 2026-07-14
- **Contexto:** Los seis endpoints específicos están implementados, pero el
  propietario necesita continuar ejecutando consultas controladas durante la
  validación y el despliegue inicial.
- **Decisión:** Conservar `GET/POST /query` durante el piloto, sin incorporarlo al
  contrato ETL. Mantener WinBridgeApi en loopback, acceder únicamente mediante el
  túnel SSH y exigir que la cuenta SQL efectiva tenga permisos de solo lectura.
- **Consecuencias:** Se acepta temporalmente una superficie de alto privilegio. La
  seguridad depende de los permisos SQL y del límite de red; `/query` deberá
  retirarse o protegerse explícitamente antes de la aceptación productiva final.

## ADR-014 — Compatibilidad con Python 3.10 de Ubuntu 22.04

- **Estado:** aceptada
- **Fecha:** 2026-07-14
- **Contexto:** El servidor ETL utiliza Ubuntu 22.04.5 LTS y su runtime nativo es
  Python 3.10.12. Mantener el requisito 3.11 obligaría a operar un runtime paralelo
  o incorporar un repositorio externo sin que el código use funciones exclusivas
  de 3.11.
- **Decisión:** Establecer Python 3.10 como versión mínima soportada y validar el
  paquete en el propio Ubuntu antes de registrar el servicio.
- **Consecuencias:** Se reduce complejidad operativa y se aprovechan las
  actualizaciones de seguridad de la distribución. Toda evolución deberá conservar
  pruebas en Python 3.10 mientras Ubuntu 22.04 sea la plataforma productiva.

## ADR-015 — Credencial PostgreSQL fuera del archivo de entorno

- **Estado:** aceptada
- **Fecha:** 2026-07-14
- **Contexto:** El servicio ETL necesita autenticarse sin interacción, pero incluir
  la contraseña en `POSTGRES_DSN` dentro de `sico-etl.env` la expondría a cualquier
  lectura accidental de la configuración o diagnóstico del entorno.
- **Decisión:** Mantener en `sico-etl.env` un DSN sin contraseña y definir
  `PGPASSFILE=/etc/sico-etl/pgpass`. El archivo `pgpass` pertenece al usuario Linux
  `sico-etl`, usa modo `0600` y contiene únicamente la credencial local para
  `127.0.0.1:5432/dap` y el rol `sico_etl`.
- **Consecuencias:** La configuración operativa puede inspeccionarse sin revelar el
  secreto y libpq/psycopg autentican sin prompt. La rotación de contraseña debe
  actualizar PostgreSQL y `pgpass` coordinadamente; el archivo sigue siendo un
  secreto que debe excluirse de Git, backups no cifrados y salidas de diagnóstico.

## ADR-016 — Excluir clientes sin RUC de la extracción

- **Estado:** aceptada
- **Fecha:** 2026-07-14
- **Contexto:** La validación real encontró 35 de 6 327 filas de `m_client` con
  `cdg_alt` vacío y ninguna con valor nulo. El contrato exige `taxId` y el endpoint abortó para no
  publicar clientes incompletos.
- **Decisión:** Filtrar en SICO con `ISNULL(cdg_alt, '') <> ''`. El responsable funcional
  confirmó que un cliente sin RUC no puede cotizar ni facturar y, por tanto, no
  pertenece a la proyección.
- **Consecuencias:** Los clientes con RUC nulo o vacío no llegan al contrato ni a
  PostgreSQL. La medición posterior de duplicados se resolvió mediante ADR-019;
  una futura inclusión de clientes sin RUC requerirá sustituir esta decisión.

## ADR-017 — Excluir grupos de artículos con código duplicado

- **Estado:** aceptada
- **Fecha:** 2026-07-14
- **Contexto:** El snapshot de `VW_Articulo` contiene 14 299 filas y 6 grupos con
  `ArtCod` duplicado. No existen códigos vacíos ni mayores de 20 caracteres. Elegir
  la primera fila haría depender el resultado de un orden no respaldado por una
  regla funcional.
- **Decisión:** WinBridgeApi publica únicamente artículos con `key_count = 1` y
  excluye todas las filas de cada grupo duplicado. No deduplicar ni escoger filas
  en Python. El propietario de SICO es responsable de corregir la calidad del
  origen.
- **Consecuencias:** Los códigos ambiguos no llegan al contrato ni a PostgreSQL.
  Como la política de snapshots es conservadora, su ausencia no desactiva ni
  elimina filas locales. La cantidad omitida debe permanecer observable en la
  validación operativa.

## ADR-018 — Usar una fecha centinela para clientes sin fecha de ingreso

- **Estado:** aceptada
- **Fecha:** 2026-07-14
- **Contexto:** El snapshot de clientes contiene filas elegibles cuyo `ing_cli` es
  nulo. El contrato exige `sourceCreatedAt` y no existe otra fecha histórica
  confirmada en SICO.
- **Decisión:** La consulta usa
  `ISNULL(ing_cli, DATETIMEFROMPARTS(2000,1,1,8,0,0,0))`, expresión validada en el
  SQL Server 2012 de destino. WinBridgeApi interpreta
  el resultado como hora de Lima antes de normalizarlo a UTC. La fecha centinela
  indica que la fecha histórica es desconocida.
- **Consecuencias:** El fallback es estable y evita actualizaciones repetidas por
  usar la hora de cada extracción. Los consumidores deben tratar
  `2000-01-01 08:00:00` en hora Lima como valor centinela, no como creación real.

## ADR-019 — Excluir grupos de clientes con RUC duplicado

- **Estado:** aceptada
- **Fecha:** 2026-07-14
- **Contexto:** Después de excluir RUC vacíos, `m_client` contiene 18 grupos de RUC
  duplicado, con 36 filas y 18 excedentes. PostgreSQL posee el índice único
  `ix_clientes_ruc` y no puede publicar ambas filas de cada grupo.
- **Decisión:** WinBridgeApi publica únicamente clientes con `tax_id_count = 1` y
  excluye todas las filas de cada grupo duplicado. No seleccionar arbitrariamente
  un cliente ni resolver la ambigüedad en Python.
- **Consecuencias:** El snapshot esperado baja de 6 292 a 6 256 clientes. SICO
  conserva la responsabilidad de corregir los RUC duplicados y la ausencia no
  provoca eliminación ni desactivación local.

## ADR-020 — Habilitar la proyección operativa sin semántica comercial adicional

- **Estado:** aceptada
- **Fecha:** 2026-07-14
- **Contexto:** Los seis endpoints, claves naturales, filtros de calidad y
  recorrido completo fueron validados técnicamente. Moneda, impuestos, vigencia,
  descuentos y una interpretación adicional de stock no forman parte del alcance
  actual ni se derivan de campos nuevos.
- **Decisión:** Sustituir la condición de ADR-008 que mantenía todos los mappings
  bloqueados. En `sico-etl` 0.1.5 se habilitan los seis mappings y se publican los
  campos existentes sin cálculo ni interpretación comercial adicional. Se
  registra `sico-etl.service`, se habilita su timer y se observa la primera
  ejecución.
- **Consecuencias:** La proyección PostgreSQL queda operativa. Las pruebas
  aisladas, el endurecimiento de seguridad y cualquier enriquecimiento funcional
  se mantienen como mejoras posteriores, no como bloqueos de operación.

## Plantilla

```text
## ADR-NNN — Título

- Estado: propuesta | aceptada | sustituida | rechazada
- Fecha: AAAA-MM-DD
- Contexto:
- Decisión:
- Consecuencias:
```
