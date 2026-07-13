# Hoja de ruta y estado

## Estado resumido

**Fase actual:** descubrimiento técnico y validación del puente.

La API mínima y los scripts de despliegue existen. Falta relevar el esquema real
del ERP y convertir el endpoint SQL temporal en un contrato de extracción.

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

## Fase 1 — Descubrimiento del ERP

- [x] Confirmar versión y edición de SQL Server.
- [ ] Confirmar edición y nivel de parches exactos de Windows Server 2012.
- [ ] Confirmar cadena de conexión efectiva y usuario de solo lectura.
- [ ] Identificar tablas o vistas de artículos, precios y stock.
- [ ] Confirmar claves y relaciones mediante consultas de muestra.
- [ ] Acordar semántica de precio, moneda, impuestos, stock y almacén.
- [ ] Medir volumen de datos y duración de las consultas.
- [ ] Determinar si existe una marca confiable para extracción incremental.

## Fase 2 — Contrato v1

- [ ] Definir DTO de precios y stock con ejemplos reales anonimizados.
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
- [ ] Añadir pruebas unitarias y de integración posibles sin el ERP real.

## Fase 4 — Despliegue y aceptación

- [x] Publicar en modo Release y desplegar con configuración externa.
- [ ] Ejecutar con una cuenta de servicio de privilegio mínimo.
- [x] Validar acceso por túnel desde Ubuntu.
- [ ] Validar que el puerto no es accesible por otras interfaces.
- [ ] Probar timeout, caída de SQL Server, reinicio del servicio y recuperación.
- [ ] Comparar muestras de precios y stock contra el ERP.
- [ ] Documentar operación, rollback y responsables.

## Próximo paso recomendado

Realizar el relevamiento de las tablas/vistas y obtener consultas de solo lectura
que representen correctamente precios y stock. No estabilizar el contrato HTTP
hasta conocer esa semántica y el volumen real.

## Bloqueos actuales

- Falta información del esquema y reglas del ERP.
- Falta relevar el esquema y las reglas funcionales de precios y stock del ERP.
