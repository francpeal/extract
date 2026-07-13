# Contexto y alcance

## Problema

La aplicación consumidora está alojada en un servidor Ubuntu remoto y necesita
obtener información del ERP del cliente, inicialmente precios y stock de
artículos. El ERP utiliza SQL Server 2012 y la conexión directa desde Ubuntu ha
presentado problemas de compatibilidad.

No se pueden realizar modificaciones en el SQL Server ni en el ERP del cliente.

## Solución elegida

Ejecutar `WinBridgeApi` en el entorno Windows del cliente, cerca de SQL Server. La
API usa el cliente .NET para conectarse localmente a la base de datos y publica
una interfaz HTTP en localhost. La aplicación Ubuntu llega a esa interfaz mediante
el túnel SSH existente.

## Objetivo

Proveer un canal estable, controlado y observable para extraer datos del ERP sin:

- exponer SQL Server a Internet;
- depender desde Ubuntu del protocolo o los controladores del SQL Server antiguo;
- modificar el ERP o su base de datos;
- introducir lógica de escritura en el ERP.

## Alcance inicial

- Consultar artículos y sus identificadores relevantes.
- Extraer clientes, almacenes y listas de precios pertinentes para la aplicación.
- Extraer precios de artículos.
- Extraer stock por la granularidad que se confirme con el negocio.
- Transportar los resultados como JSON hacia la aplicación Ubuntu.
- Sincronizar una proyección de solo lectura en PostgreSQL mediante un ETL Linux.
- Operar a través del túnel SSH restringido al servidor Ubuntu autorizado.
- Disponer de diagnóstico de salud y registros operativos suficientes.

## Fuera de alcance inicial

- Crear o actualizar datos del ERP.
- Sustituir funcionalidades del ERP.
- Exponer públicamente la API.
- Construir una plataforma genérica para ejecutar SQL remoto.
- Sincronización bidireccional o en tiempo real, hasta que se solicite y diseñe.

## Restricciones conocidas

- SQL Server 2012 y esquema controlado por un tercero.
- Sin autorización para modificar el servidor de base de datos o el ERP.
- La API debe ejecutarse en la infraestructura disponible del cliente.
- El transporte entre Ubuntu y el cliente depende del túnel SSH.
- Los nombres de tablas, vistas y reglas de precios/stock aún no están documentados.

## Criterios de éxito del primer hito

1. Ubuntu consulta un endpoint estable a través del túnel.
2. La API obtiene precios y stock sin modificar datos del ERP.
3. El contrato identifica inequívocamente artículo, precio, moneda, almacén y
   fecha de extracción cuando dichos conceptos apliquen.
4. Fallos de conexión, timeout o consulta son diagnosticables.
5. No existe acceso al endpoint desde interfaces de red no previstas.

## Pendientes de definición funcional

- Identificador canónico del artículo: código, SKU u otro.
- Lista o tipo de precio requerido, moneda e impuestos.
- Stock físico, disponible, comprometido u otra definición.
- Granularidad por almacén/sucursal y almacenes incluidos.
- Frecuencia de consulta y volumen aproximado.
- Extracción completa o incremental y campo confiable para el cursor.
- Tratamiento de artículos inactivos y valores nulos.
- Clave canónica y política de datos personales para clientes.
- Política de ausencia por entidad: conservar, desactivar o eliminar.
