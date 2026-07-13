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

- **Estado:** propuesta, pendiente de confirmación
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

## Plantilla

```text
## ADR-NNN — Título

- Estado: propuesta | aceptada | sustituida | rechazada
- Fecha: AAAA-MM-DD
- Contexto:
- Decisión:
- Consecuencias:
```
