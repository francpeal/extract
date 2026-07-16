# Extract / WinBridgeApi / SICO ETL

Puente de integración para consultar precios y stock del ERP de un cliente desde
una aplicación alojada en Ubuntu, sin conectar directamente ese servidor con la
instancia antigua de SQL Server.

La solución ejecuta una API ASP.NET Core junto al ERP. La API accede localmente a
SQL Server y se consume desde Ubuntu a través de un túnel SSH restringido.

## Documentación de referencia

Antes de modificar el proyecto, leer en este orden:

1. [Contexto y alcance](docs/PROJECT.md)
2. [Arquitectura](docs/ARCHITECTURE.md)
3. [Contrato de integración](docs/API.md)
4. [Seguridad y operación](docs/SECURITY.md)
5. [Decisiones de arquitectura](docs/DECISIONS.md)
6. [Hoja de ruta y estado](docs/ROADMAP.md)
7. [Protocolo de sesiones](docs/SESSIONS.md)
8. [Procedimientos operativos y migración](docs/PROCEDURES.md)
9. [Matriz de mappings del ETL](docs/ETL_MAPPINGS.md)

Para continuar los pendientes funcionales de descubrimiento y aceptación, usar
el [procedimiento de relevamiento del ERP](docs/ERP_DISCOVERY.md).

Las instrucciones de despliegue actuales están en
[WinBridgeApi/DEPLOY.md](WinBridgeApi/DEPLOY.md).

La base del sincronizador Linux está en [etl/README.md](etl/README.md) y la matriz
de campos confirmados y pendientes en [docs/ETL_MAPPINGS.md](docs/ETL_MAPPINGS.md).
El procedimiento reproducible de instalación, permisos y credenciales está en
[etl/DEPLOY_UBUNTU.md](etl/DEPLOY_UBUNTU.md).

## Estado actual

WinBridgeApi está desplegado como servicio Windows y los seis endpoints recorren
snapshots completos a través del túnel. `sico-etl` 0.1.5 publica de forma
idempotente las seis entidades a PostgreSQL mediante un servicio `oneshot`
registrado, invocado por un timer `systemd` habilitado. La evidencia productiva del 2026-07-15 confirma
sincronizaciones `snapshot` exitosas cada cinco minutos, con 53 469 filas en 111
páginas, sin rechazos ni cambios pendientes de aplicar.

Las restricciones y claves naturales de PostgreSQL están confirmadas. Precios y
stock se transfieren tal como los expone SICO: no se agregan ni se interpretan
campos de moneda, impuestos, vigencia o disponibilidad fuera del alcance actual.
Los temas de seguridad y las pruebas aisladas restantes se conservan como mejoras
no bloqueantes. El punto exacto de reanudación está en
[docs/ROADMAP.md](docs/ROADMAP.md).
