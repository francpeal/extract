# SICO ETL

Aplicación Python independiente para extraer seis recursos desde WinBridgeApi y
proyectarlos en PostgreSQL. SICO continúa siendo la fuente de verdad.

## Estado de seguridad

La extracción y validación en modo `--dry-run` están implementadas. La publicación
se bloquea deliberadamente porque los mappings SICO y las restricciones naturales
de PostgreSQL todavía no fueron confirmados. Cada contrato mantiene
`mapping_confirmed=False`; cambiarlo requiere evidencia, pruebas y una decisión
documentada.

Los endpoints `/api/v1/extract/*` son contratos objetivo: WinBridgeApi aún no los
implementa porque falta confirmar el esquema de SICO. `/query` no es consumido por
este proyecto.

## Requisitos

- Python 3.11 o superior.
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

Los archivos de `deploy/systemd/` son plantillas y no deben activarse mientras los
mappings estén pendientes. Una vez aprobados:

1. Instalar el proyecto en `/opt/sico-etl` con un usuario dedicado.
2. Crear `/etc/sico-etl/sico-etl.env` con modo `0600`.
3. Aplicar las migraciones con una cuenta autorizada para DDL.
4. Ejecutar primero `--dry-run` y comparar las seis muestras con SICO.
5. Copiar y revisar las unidades systemd.
6. Habilitar el timer solo después de una ejecución completa satisfactoria.

El intervalo de cinco minutos incluido en la plantilla es ilustrativo, no una
frecuencia aprobada. Debe ajustarse después de medir las seis extracciones. El
umbral de reducción predeterminado es 0%: cualquier snapshot menor se bloquea
hasta acordar una política de ausencias.

Diagnóstico previsto:

```bash
systemctl status sico-etl.timer sico-etl.service --no-pager
journalctl -u sico-etl.service -n 100 --no-pager
```

El rollback consiste en deshabilitar el timer. Una extracción fallida no debe
reemplazar el último conjunto válido.
