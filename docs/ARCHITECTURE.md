# Arquitectura

## Vista de contexto

```text
Aplicación web (Ubuntu) ---> PostgreSQL (proyección local)
                                  ^
                                  | transacciones idempotentes
                             SICO ETL (Ubuntu)
                                  |
                                  | HTTP dentro de túnel SSH
                                  v
                        WinBridgeApi (Windows cliente)
                                  |
                                  | Microsoft.Data.SqlClient / red local
                                  v
                          SQL Server 2012 / SICO
```

## Responsabilidades

### Aplicación Ubuntu

- Iniciar o utilizar el túnel SSH.
- Invocar el contrato HTTP.
- Programar extracciones, reintentos y persistencia en el sistema de destino.
- Interpretar el contrato de datos, no el esquema interno del ERP.

### SICO ETL

- Orquestar las seis extracciones, paginación y reintentos.
- Validar DTO, decimales, códigos y timestamps antes de escribir.
- Publicar por entidad de forma idempotente y transaccional en PostgreSQL.
- Evitar ejecuciones solapadas mediante advisory lock.
- Registrar ejecuciones, métricas, checkpoints y rechazos sin datos sensibles.
- Conservar la última proyección válida ante fallos o umbrales anómalos.

### PostgreSQL

- Proyección local optimizada para la aplicación comercial; no sustituye a SICO
  como fuente de verdad.
- Conserva los identificadores locales y se actualiza por claves naturales una
  vez confirmadas.
- Aloja tablas de control del ETL separadas de las seis tablas de negocio.

### WinBridgeApi

- Encapsular la conectividad compatible con SQL Server.
- Ejecutar únicamente las consultas de lectura definidas para la integración.
- Traducir tipos y resultados SQL a un contrato JSON estable.
- Aplicar timeout, cancelación, límites y manejo consistente de errores.
- Proveer salud y logging sin filtrar secretos o datos innecesarios.

### SQL Server / ERP

- Fuente de verdad para artículos, precios y stock.
- No recibe cambios de esquema ni lógica por parte de este proyecto.
- Debe ser accedido mediante un usuario dedicado de solo lectura cuando sea posible.

## Vista de despliegue objetivo

- `WinBridgeApi`: ASP.NET Core 8 como servicio de Windows.
- Escucha objetivo: `127.0.0.1:5000`, salvo que la topología del túnel obligue a
  otra interfaz explícitamente documentada.
- SQL Server: conexión configurada, actualmente `localhost:1998`.
- Transporte externo: túnel SSH limitado a la IP origen del servidor Ubuntu.
- Forwarding validado: `Ubuntu 127.0.0.1:15000` hacia
  `Windows 127.0.0.1:5000`.
- Persistencia del túnel: servicio `systemd` `winbridge-tunnel.service`, habilitado
  al arranque y con reinicio automático.
- Configuración sensible: fuera del control de versiones.

## Arquitectura actual frente a objetivo

| Tema | Estado actual | Estado objetivo |
|---|---|---|
| Interfaz HTTP | Kestrel en localhost | Kestrel en localhost |
| Acceso a datos | SQL arbitrario en `/query` | Operaciones de lectura específicas |
| Contrato | Filas SQL genéricas | DTO estable de precios y stock |
| Permisos SQL | Dependen de configuración | Usuario dedicado de solo lectura |
| Salud | Confirma que el proceso vive | Salud básica y diagnóstico separado de BD |
| Servicio | `WindowsServiceLifetime`, ejecutado como `LocalSystem` | Cuenta de servicio con privilegios mínimos |
| Transporte | HTTP plano | HTTP exclusivamente dentro de SSH |
| Persistencia Ubuntu | Base ETL creada, aún sin desplegar | ETL Linux hacia PostgreSQL |
| Orquestación | Manual / consumidor | CLI y timer systemd dedicado |
| Escritura ETL | No existe | Bloqueada hasta confirmar mappings; luego transaccional |

## Flujo principal

1. El ETL solicita una página de una de las seis entidades mediante el túnel.
2. WinBridgeApi valida los parámetros del contrato.
3. La API ejecuta una consulta conocida y parametrizada.
4. SQL Server devuelve las filas solicitadas.
5. La API normaliza los datos y responde JSON.
6. El ETL valida todas las páginas y los umbrales de seguridad.
7. PostgreSQL publica atómicamente los upserts de la entidad.
8. La aplicación web continúa leyendo localmente aunque SICO esté temporalmente
   indisponible.

## Principios de evolución

- Mantener la API sin estado.
- Separar contrato externo de nombres y detalles del esquema ERP.
- Evitar abstracciones prematuras: agregar capas cuando existan consultas reales.
- Favorecer paginación o extracción incremental para conjuntos grandes.
- Mantener las escrituras al ERP fuera de esta solución.
- No convertir ausencia en SICO en eliminación local sin una política aprobada.
- Bloquear publicación cuando claves, volumen o mapping no estén confirmados.
