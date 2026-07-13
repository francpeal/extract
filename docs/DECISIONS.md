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

## ADR-009 — Snapshot conservador antes que incremental no demostrado

- **Estado:** aceptada para la primera puesta en marcha
- **Fecha:** 2026-07-13
- **Contexto:** No se confirmó una marca estable de modificación en SICO.
- **Decisión:** Preparar snapshots paginados con límites y controles de volumen.
  `updatedSince` permanece deshabilitado por entidad hasta demostrar un cursor
  confiable. La ausencia de una fila no provoca borrado ni desactivación.
- **Consecuencias:** Se favorece consistencia y recuperación a costa de releer más
  datos. La estrategia podrá evolucionar por entidad con mediciones reales.

## ADR-010 — ETL Python independiente y operado por systemd

- **Estado:** aceptada
- **Fecha:** 2026-07-13
- **Contexto:** La orquestación, PostgreSQL y el túnel residen en Linux; no deben
  incorporarse al ciclo de vida de la aplicación web ni de WinBridgeApi.
- **Decisión:** Implementar `etl/` como paquete Python 3.11+ con CLI, configuración
  por entorno y plantillas `systemd` de tipo oneshot/timer.
- **Consecuencias:** Despliegue y rollback independientes. Python y psycopg pasan a
  ser dependencias operativas del sincronizador Linux.

## Plantilla

```text
## ADR-NNN — Título

- Estado: propuesta | aceptada | sustituida | rechazada
- Fecha: AAAA-MM-DD
- Contexto:
- Decisión:
- Consecuencias:
```
