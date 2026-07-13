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

Para continuar la fase actual de descubrimiento, usar el
[procedimiento de relevamiento del ERP](docs/ERP_DISCOVERY.md).

Las instrucciones de despliegue actuales están en
[WinBridgeApi/DEPLOY.md](WinBridgeApi/DEPLOY.md).

La base del sincronizador Linux está en [etl/README.md](etl/README.md) y la matriz
de campos confirmados y pendientes en [docs/ETL_MAPPINGS.md](docs/ETL_MAPPINGS.md).

## Estado actual

Existe una prueba funcional de la API con endpoints de salud y ejecución temporal
de SQL. El proyecto ETL ya dispone de contratos preliminares, validación, CLI,
controles de publicación y pruebas aisladas. Los mappings de SICO y las claves de
PostgreSQL aún deben confirmarse antes de habilitar escrituras.
