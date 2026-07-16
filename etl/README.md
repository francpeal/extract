# SICO ETL

Aplicación Python independiente para extraer seis recursos desde WinBridgeApi y
proyectarlos en PostgreSQL. SICO continúa siendo la fuente de verdad.

## Estado de seguridad

La versión 0.1.5 habilita la publicación de las seis entidades mediante upsert
transaccional. El `dry-run` previo recorrió 53 462 filas en 111 páginas, sin
rechazos ni escrituras. Las restricciones naturales de PostgreSQL están
confirmadas. Los datos de precios y stock se transfieren tal como están expuestos
por WinBridgeApi, sin interpretar ni derivar información adicional.

La operación productiva fue confirmada el 2026-07-15. El timer de cinco minutos
ejecutó snapshots exitosos de 53 469 filas en 111 páginas: 20 almacenes, 13
listas, 14 289 artículos, 6 256 clientes, 18 032 precios y 14 859 stocks. No hubo
rechazos; en la última ejecución observada las filas ya estaban alineadas y no
requirieron inserciones ni actualizaciones.

Los seis endpoints específicos y sus snapshots completos están validados
técnicamente. `/query` no es consumido por este proyecto.

El cliente acepta timestamps ISO 8601 emitidos por .NET con siete dígitos de
fracción de segundo y los normaliza a la precisión de microsegundos de Python.

## Requisitos

- Python 3.10 o superior.
- PostgreSQL accesible desde Linux.
- Túnel `winbridge-tunnel.service` activo en `127.0.0.1:15000`.
- Endpoints específicos implementados en WinBridgeApi.

## Desarrollo

```bash
cd etl
python3 -m venv .venv
. .venv/bin/activate
python -m pip install -e .
cp .env.example .env
```

El proyecto no carga automáticamente `.env`. En desarrollo se puede exportar de
forma controlada; en producción systemd utiliza `EnvironmentFile`.

Pruebas sin ERP ni PostgreSQL:

```bash
python -m unittest discover -s tests -v
```

## Ejecución

Validar una entidad sin escribir en PostgreSQL:

```bash
sico-etl run --entity articles --dry-run
```

Validar las seis entidades en orden de dependencia preliminar:

```bash
sico-etl run --entity all --dry-run
```

La opción incremental está implementada en el CLI, pero cada contrato la rechaza
hasta confirmar un campo de cambio estable en SICO:

```bash
sico-etl run --entity articles --updated-since 2026-07-13T00:00:00Z --dry-run
```

## Códigos de salida

| Código | Significado |
|---:|---|
| 0 | ejecución satisfactoria |
| 2 | configuración o contrato inválido |
| 3 | fallo de WinBridgeApi |
| 4 | publicación bloqueada por mapping no confirmado |
| 5 | fallo de dependencia o PostgreSQL |
| 75 | otra ejecución posee el advisory lock |
| 130 | cancelación solicitada mediante SIGINT o SIGTERM |

## Publicación y consistencia

El repositorio PostgreSQL:

- preserva el `id` local y localiza filas por la clave natural confirmada;
- ejecuta todos los upserts de una entidad en una transacción;
- usa `IS DISTINCT FROM` para no actualizar filas idénticas;
- asigna `updated_at` desde `sourceUpdatedAt`, cuando exista, o desde la hora de
  sincronización;
- rechaza cualquier escritura mientras el mapping no esté confirmado;
- rechaza claves vacías/duplicadas, snapshots demasiado pequeños, caídas de
  volumen superiores al umbral y extracciones que excedan el máximo configurado.

La migración `migrations/001_etl_control.sql` crea ejecución, detalle por entidad,
checkpoints, rechazos y staging genérico. No modifica las seis tablas de negocio.
El staging puede contener datos personales y marca vencimiento a las 24 horas; la
operación debe purgar filas vencidas y limitar acceso a la cuenta del ETL.

La purga explícita está en `scripts/purge_expired_staging.sql`; no debe ejecutarse
contra SICO ni incluirse en WinBridgeApi.

Antes de habilitar escrituras se debe decidir cómo separar `source_updated_at`,
`extracted_at` y `synced_at`. Hasta entonces, la trazabilidad vive en las tablas de
control.

## Activación en Linux

El procedimiento completo y validado, incluidos usuario Linux, rol PostgreSQL,
permisos, claves, índices y `PGPASSFILE`, está en
[`DEPLOY_UBUNTU.md`](DEPLOY_UBUNTU.md).

Los archivos de `deploy/systemd/` están instalados para `sico-etl` 0.1.5 y el
timer de cinco minutos está habilitado en producción. El umbral de reducción
predeterminado es 0%: cualquier snapshot menor se bloquea.

Diagnóstico previsto:

```bash
sudo bash /opt/sico-etl/scripts/validate_ubuntu.sh
systemctl status sico-etl.timer sico-etl.service --no-pager
journalctl -u sico-etl.service -n 100 --no-pager
```

`validate_ubuntu.sh` es de solo lectura. Exige que el túnel y el timer estén
activos y habilitados, y que el servicio `oneshot` esté registrado y haya
finalizado correctamente en su última ejecución. Comprueba la versión esperada
(por defecto, `0.1.5`), la configuración no secreta, el acceso a `/health` y los
metadatos de la última sincronización. Para validar otra versión aprobada, usar
`EXPECTED_ETL_VERSION=x.y.z`.

El rollback consiste en deshabilitar el timer. Una extracción fallida no debe
reemplazar el último conjunto válido.
