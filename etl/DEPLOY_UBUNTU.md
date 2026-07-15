# Despliegue del ETL SICO en Ubuntu

## Propósito y alcance

Esta guía permite instalar y verificar el ETL que proyecta datos de SICO en la
base PostgreSQL `dap`. Describe el procedimiento validado en el servidor Ubuntu,
incluyendo la cuenta Linux, el entorno virtual, las tablas de control, la cuenta
PostgreSQL de privilegio mínimo y el almacenamiento de la contraseña fuera del
archivo de variables de entorno.

No incluye secretos reales. Esta guía registra el artefacto efectivo del cierre;
en una actualización futura, los nombres y sumas deben corresponder al nuevo
paquete aprobado.

El ETL consume WinBridgeApi únicamente mediante el túnel local:

```text
sico-etl -> http://127.0.0.1:15000 -> túnel SSH -> WinBridgeApi -> SICO
         -> PostgreSQL 127.0.0.1:5432/dap
```

## Estado validado el 2026-07-14

| Componente | Estado comprobado |
|---|---|
| Sistema operativo | Ubuntu 22.04.5 LTS, x86_64 |
| Python | 3.10.12 del sistema, usado dentro de un `venv` |
| PostgreSQL | 17.10, base `dap` local |
| ETL instalado | `sico-etl` 0.1.4 en `/opt/sico-etl`; actualización operativa a 0.1.5 pendiente |
| Usuario Linux | `sico-etl`, sin uso interactivo |
| Usuario PostgreSQL | `sico_etl`, `LOGIN`, límite de 2 conexiones |
| WinBridgeApi | `/health` responde por `127.0.0.1:15000` |
| Endpoints ETL | Seis snapshots completos validados desde Ubuntu mediante el túnel |
| Tablas de control | Cinco tablas creadas en `public` |
| Pruebas Python | 31 ejecutadas: 30 OK y 1 integración omitida |
| Servicio/timer ETL | No registrados ni habilitados todavía |

La versión Windows con los seis endpoints fue desplegada el 2026-07-14 y los seis
snapshots completos se validaron desde Ubuntu. `sico-etl` 0.1.5 habilita la
publicación hacia las tablas de negocio sin transformar semánticamente precios ni
stock. Tras instalarla, registrar el servicio, observar una ejecución manual y
habilitar el timer de cinco minutos.

El primer `dry-run` completo con 0.1.3 se detuvo sin publicar porque Python 3.10
no aceptó la precisión de siete dígitos de `extractedAt` emitida por .NET. La
versión 0.1.4 normaliza esa fracción a microsegundos y ya está instalada.

Con 0.1.4, almacenes y listas completaron el `dry-run`. Artículos detectó 6 grupos
de `ArtCod` duplicados entre 14 299 filas. Se excluyeron todos esos grupos con
`key_count = 1` en la versión de WinBridgeApi ya desplegada.

Después del filtro de artículos, su `dry-run` completó 14 284 filas en 29 páginas.
Clientes encontró valores `ing_cli` nulos. Se aprobó usar la fecha centinela fija
`2000-01-01 08:00:00`, que indica fecha histórica desconocida y evita cambios
entre ejecuciones.

El snapshot inicial de clientes completó 6 292 filas y luego se midieron 18 grupos de RUC
duplicado, con 36 filas. Se aprobó excluir todos esos grupos mediante
`tax_id_count = 1`. Después del parche, clientes completó 6 256 filas y el
`dry-run` integrado volvió a finalizar sin rechazos.

El `dry-run` integrado final de 0.1.4 completó el 2026-07-14 las seis entidades en
aproximadamente 114 segundos, sin rechazos ni escrituras:

| Entidad | Filas | Páginas |
|---|---:|---:|
| Almacenes | 20 | 1 |
| Listas de precios | 13 | 1 |
| Artículos | 14 284 | 29 |
| Clientes | 6 256 | 13 |
| Precios | 18 030 | 37 |
| Stock por almacén | 14 859 | 30 |

Artefactos de esta instalación:

- paquete `/home/sico-etl-0.1.4-pilot-20260714-140438.zip`, SHA-256
  `5f3a79afecb5da2a178dd74bb727071047ef8c9dbbe15dcf0020b621b346000a`;
- wheel `/opt/sico-etl/sico_etl-0.1.4-py3-none-any.whl`, SHA-256
  `1d99e413412dc3daf70fa4090e9b4e06cfc40a9126ece5e28d6f50de55608a97`;
- respaldo `/root/sico-etl-backups/sico-etl-0.1.3-files-20260714-141230.tar.gz`,
  SHA-256 `43d4ff3a5fefc252fb78690ad22ce5e4f805c924e9f95e8ad86a76a74a89232a`.

## 1. Comprobar prerrequisitos

Ejecutar como `root`:

```bash
cat /etc/os-release
uname -m
python3 --version
python3 -m venv --help >/dev/null && echo "venv disponible"
psql --version
systemctl is-active winbridge-tunnel.service
systemctl is-enabled winbridge-tunnel.service
```

Valores esperados en el servidor validado:

- Ubuntu 22.04 LTS y arquitectura `x86_64`;
- Python 3.10 o superior;
- `venv disponible`;
- túnel `active` y `enabled`.

No reemplazar `/usr/bin/python3` ni migrar globalmente el Python del sistema. El
ETL utiliza un entorno virtual aislado.

Comprobar el túnel y la API:

```bash
curl -i --max-time 10 http://127.0.0.1:15000/health
ss -lntp | grep ':15000'
```

`/health` debe responder HTTP 200 y el puerto 15000 debe escuchar solamente en
`127.0.0.1`. Los seis endpoints ya fueron validados con la versión desplegada.

## 2. Verificar y copiar el paquete

Ejemplo con el paquete piloto validado:

```bash
PACKAGE=/home/sico-etl-0.1.4-pilot-20260714-140438.zip
sha256sum "$PACKAGE"
unzip -l "$PACKAGE"
```

La suma validada para ese archivo fue:

```text
5f3a79afecb5da2a178dd74bb727071047ef8c9dbbe15dcf0020b621b346000a
```

No continuar si la suma no coincide con la comunicada por el responsable de la
compilación. El ZIP debe contener, como mínimo, el wheel, `README.md`,
`.env.example`, `migrations/`, `scripts/`, `deploy/systemd/` y `tests/`.

## 3. Crear la identidad Linux y los directorios

La cuenta Linux usa guion (`sico-etl`); la cuenta PostgreSQL usa guion bajo
(`sico_etl`). Son identidades diferentes.

Crear la cuenta solamente si no existe:

```bash
id sico-etl >/dev/null 2>&1 || \
  adduser --system --group --no-create-home --home /nonexistent sico-etl
```

Crear las rutas con acceso restringido:

```bash
install -d -o root -g sico-etl -m 0750 /opt/sico-etl
install -d -o root -g sico-etl -m 0750 /etc/sico-etl
install -d -o sico-etl -g sico-etl -m 0750 /var/log/sico-etl
```

Extraer el paquete y normalizar propietarios. La extracción inicial debe hacerse
con `/opt/sico-etl` vacío; en una actualización se debe conservar `.venv` y usar
el procedimiento de actualización de la sección 14.

```bash
unzip "$PACKAGE" -d /opt/sico-etl
chown -R root:sico-etl /opt/sico-etl
find /opt/sico-etl -type d -exec chmod 0750 {} \;
find /opt/sico-etl -type f -exec chmod 0640 {} \;
```

Verificar:

```bash
id sico-etl
ls -ld /opt/sico-etl /etc/sico-etl /var/log/sico-etl
ls -l /opt/sico-etl
```

## 4. Crear el entorno virtual e instalar el wheel

```bash
python3 -m venv /opt/sico-etl/.venv
/opt/sico-etl/.venv/bin/python -m pip install --upgrade pip
/opt/sico-etl/.venv/bin/pip install \
  /opt/sico-etl/sico_etl-0.1.4-py3-none-any.whl \
  'psycopg[binary]'
chown -R root:sico-etl /opt/sico-etl/.venv
chmod 0750 /opt/sico-etl/.venv
```

En una versión posterior, sustituir el nombre del wheel. No instalar el paquete
en el Python global del servidor.

Comprobar instalación:

```bash
/opt/sico-etl/.venv/bin/python --version
/opt/sico-etl/.venv/bin/pip show sico-etl psycopg psycopg-binary
```

## 5. Ejecutar las pruebas del paquete

```bash
cd /opt/sico-etl
runuser -u sico-etl -- \
  /opt/sico-etl/.venv/bin/python -m unittest discover -s tests -v
```

En la instalación 0.1.4 se obtuvo:

```text
Ran 31 tests
OK (skipped=1)
```

La prueba omitida requiere `TEST_POSTGRES_DSN` y debe ejecutarse posteriormente
contra una base PostgreSQL aislada, nunca improvisarse sobre `dap`.

## 6. Inspeccionar PostgreSQL antes de modificarlo

Confirmar servidor, base y escucha:

```bash
ss -lntp | grep ':5432'
runuser -u postgres -- psql -X -Atc "SELECT version();"
runuser -u postgres -- psql -X -Atc \
  "SELECT datname FROM pg_database WHERE datallowconn AND NOT datistemplate ORDER BY datname;"
```

En el servidor validado PostgreSQL escucha en todas las interfaces. Antes de
cerrar la aceptación productiva se deben revisar `listen_addresses`, `pg_hba.conf`
y el firewall. El ETL siempre se conecta a `127.0.0.1`.

Confirmar las seis tablas de negocio:

```bash
runuser -u postgres -- psql -X -d dap -P pager=off -c "
SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_name IN (
  'articulos', 'clientes', 'almacenes',
  'lista_precios', 'precios', 'stock_almacen'
)
ORDER BY table_schema, table_name;
"
```

Todas deben existir en `public`.

### 6.1 Verificar claves, constraints e índices

```bash
runuser -u postgres -- psql -X -d dap -P pager=off -c "
SELECT
  tc.table_name AS tabla,
  tc.constraint_name,
  tc.constraint_type AS tipo,
  pg_get_constraintdef(pc.oid) AS definicion
FROM information_schema.table_constraints AS tc
JOIN pg_constraint AS pc
  ON pc.conname = tc.constraint_name
 AND pc.connamespace = to_regnamespace(tc.constraint_schema)
WHERE tc.table_schema = 'public'
  AND tc.table_name IN (
    'articulos', 'clientes', 'almacenes',
    'lista_precios', 'precios', 'stock_almacen'
  )
ORDER BY tc.table_name, tc.constraint_type, tc.constraint_name;

SELECT tablename, indexname, indexdef
FROM pg_indexes
WHERE schemaname = 'public'
  AND tablename IN (
    'articulos', 'clientes', 'almacenes',
    'lista_precios', 'precios', 'stock_almacen'
  )
ORDER BY tablename, indexname;
"
```

Claves naturales confirmadas para el upsert:

| Tabla | Clave natural respaldada por unicidad |
|---|---|
| `articulos` | `codigo` |
| `clientes` | `cod_dap` mediante `ix_clientes_cod_dap` |
| `almacenes` | `codigo` |
| `lista_precios` | `codigo` |
| `precios` | `(cod_articulo, cod_lista)` |
| `stock_almacen` | `(cod_articulo, cod_almacen)` |

`clientes` posee además el índice único `ix_clientes_ruc`. La calidad se midió en
el origen: 18 grupos y 36 filas duplicadas se excluyen completamente mediante
`tax_id_count = 1` antes de llegar al ETL.

### 6.2 Verificar secuencias de los identificadores locales

```bash
runuser -u postgres -- psql -X -d dap -P pager=off -c "
SELECT
  table_name,
  column_default,
  is_identity,
  pg_get_serial_sequence(
    quote_ident(table_schema) || '.' || quote_ident(table_name),
    column_name
  ) AS secuencia
FROM information_schema.columns
WHERE table_schema = 'public'
  AND column_name = 'id'
  AND table_name IN (
    'articulos', 'clientes', 'almacenes',
    'lista_precios', 'precios', 'stock_almacen'
  )
ORDER BY table_name;
"
```

Se confirmaron las secuencias `articulos_id_seq`, `clientes_id_seq`,
`almacenes_id_seq`, `lista_precios_id_seq`, `precios_id_seq` y
`stock_almacen_id_seq`. El ETL preserva los IDs existentes y usa estas secuencias
solo al insertar nuevas filas.

### 6.3 Registrar la línea base de volúmenes

```bash
runuser -u postgres -- psql -X -d dap -P pager=off -c "
SELECT 'almacenes' AS tabla, count(*) AS filas FROM almacenes
UNION ALL SELECT 'articulos', count(*) FROM articulos
UNION ALL SELECT 'clientes', count(*) FROM clientes
UNION ALL SELECT 'lista_precios', count(*) FROM lista_precios
UNION ALL SELECT 'precios', count(*) FROM precios
UNION ALL SELECT 'stock_almacen', count(*) FROM stock_almacen;
"
```

La línea base observada fue 20 almacenes, 13 742 artículos, 2 clientes, 13 listas,
17 958 precios y 14 830 registros de stock. Son valores de referencia, no límites
definitivos para SICO.

Para una inspección ampliada y de solo lectura puede ejecutarse:

```bash
runuser -u postgres -- psql -X -d dap -P pager=off \
  < /opt/sico-etl/scripts/inspect_postgres.sql
```

## 7. Aplicar las tablas de control ETL

Calcular la suma del archivo que se va a aplicar:

```bash
sha256sum /opt/sico-etl/migrations/001_etl_control.sql
```

Para la versión validada fue:

```text
6c74c54451701d23818dd22eab2c508c33d057103ffafa735d6ae53eca1c0584
```

Preverificar que no existan:

```bash
runuser -u postgres -- psql -X -d dap -P pager=off -c "
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name IN (
    'etl_runs', 'etl_entity_runs', 'etl_checkpoints',
    'etl_rejections', 'etl_staging_records'
  )
ORDER BY table_name;
"
```

Aplicar la migración con parada ante el primer error:

```bash
runuser -u postgres -- psql \
  -X \
  -v ON_ERROR_STOP=1 \
  -d dap \
  < /opt/sico-etl/migrations/001_etl_control.sql
```

Se usa redirección del shell de `root` porque el usuario `postgres` no puede
atravesar `/opt/sico-etl`, protegido con modo `0750`. No ampliar permisos para
resolverlo. La migración es transaccional y no modifica las seis tablas de
negocio.

Verificar las cinco tablas:

```bash
runuser -u postgres -- psql -X -d dap -P pager=off -c "
SELECT tablename, tableowner
FROM pg_tables
WHERE schemaname = 'public'
  AND tablename LIKE 'etl_%'
ORDER BY tablename;
"
```

## 8. Crear el usuario PostgreSQL de privilegio mínimo

Crear el rol una sola vez:

```bash
runuser -u postgres -- psql -X -d dap -c "
CREATE ROLE sico_etl
  LOGIN
  NOSUPERUSER
  NOCREATEDB
  NOCREATEROLE
  NOINHERIT
  NOREPLICATION
  CONNECTION LIMIT 2;
"
```

Asignar la contraseña interactivamente para que no quede en el historial:

```bash
runuser -u postgres -- psql -X -d dap
```

Dentro de `psql`:

```text
\password sico_etl
\q
```

No pegar la contraseña en comandos, documentación, tickets ni chat.

## 9. Conceder únicamente los permisos requeridos

Ejecutar como `postgres`:

```bash
runuser -u postgres -- psql -X -v ON_ERROR_STOP=1 -d dap <<'SQL'
BEGIN;

GRANT CONNECT ON DATABASE dap TO sico_etl;
GRANT USAGE ON SCHEMA public TO sico_etl;

GRANT SELECT, INSERT, UPDATE ON TABLE
  articulos,
  clientes,
  almacenes,
  lista_precios,
  precios,
  stock_almacen
TO sico_etl;

GRANT USAGE ON SEQUENCE
  articulos_id_seq,
  clientes_id_seq,
  almacenes_id_seq,
  lista_precios_id_seq,
  precios_id_seq,
  stock_almacen_id_seq
TO sico_etl;

GRANT SELECT, INSERT, UPDATE
  ON TABLE etl_runs TO sico_etl;
GRANT SELECT, INSERT
  ON TABLE etl_entity_runs TO sico_etl;
GRANT SELECT, INSERT, UPDATE
  ON TABLE etl_checkpoints TO sico_etl;
GRANT SELECT, INSERT
  ON TABLE etl_rejections TO sico_etl;
GRANT SELECT, INSERT, UPDATE, DELETE
  ON TABLE etl_staging_records TO sico_etl;

GRANT USAGE ON SEQUENCE
  etl_entity_runs_id_seq,
  etl_rejections_id_seq
TO sico_etl;

ALTER ROLE sico_etl IN DATABASE dap SET search_path = public;

COMMIT;
SQL
```

El rol no recibe `CREATE` sobre el esquema, ni `DELETE` o `TRUNCATE` sobre las
tablas de negocio. `DELETE` se limita al staging vencido.

### 9.1 Verificar los permisos efectivos

```bash
runuser -u postgres -- psql -X -d dap -P pager=off -c "
SELECT table_name, string_agg(privilege_type, ', ' ORDER BY privilege_type) AS privilegios
FROM information_schema.role_table_grants
WHERE grantee = 'sico_etl'
GROUP BY table_name
ORDER BY table_name;

SELECT
  has_database_privilege('sico_etl', 'dap', 'CONNECT') AS puede_conectar,
  has_schema_privilege('sico_etl', 'public', 'USAGE') AS puede_usar_schema,
  has_schema_privilege('sico_etl', 'public', 'CREATE') AS puede_crear,
  has_table_privilege('sico_etl', 'articulos', 'DELETE') AS puede_borrar_articulos,
  has_table_privilege('sico_etl', 'articulos', 'TRUNCATE') AS puede_truncar_articulos;
"
```

Resultado esperado de la segunda consulta:

```text
puede_conectar = true
puede_usar_schema = true
puede_crear = false
puede_borrar_articulos = false
puede_truncar_articulos = false
```

Probar la autenticación TCP una vez:

```bash
psql -h 127.0.0.1 -U sico_etl -d dap -W \
  -c "SELECT current_user, current_database(), count(*) AS articulos FROM articulos;"
```

## 10. Crear la configuración sin incluir la contraseña

Instalar la plantilla y retirar el DSN con placeholder:

```bash
install -o root -g sico-etl -m 0640 \
  /opt/sico-etl/.env.example \
  /etc/sico-etl/sico-etl.env

sed -i '/^POSTGRES_DSN=/d' /etc/sico-etl/sico-etl.env
```

La configuración no secreta validada es:

```dotenv
WINBRIDGE_BASE_URL=http://127.0.0.1:15000
WINBRIDGE_TIMEOUT_SECONDS=30
WINBRIDGE_PAGE_LIMIT=500
WINBRIDGE_MAX_RETRIES=4
WINBRIDGE_RETRY_BASE_SECONDS=0.5
ETL_LOG_LEVEL=INFO
ETL_LOCK_ID=726493201
ETL_MIN_EXPECTED_ROWS=1
ETL_MAX_DECREASE_PERCENT=0
ETL_MAX_ROWS_PER_ENTITY=1000000
```

No configurar una URL remota para WinBridgeApi: debe usarse el puerto local del
túnel.

## 11. Guardar la contraseña en `pgpass`

La contraseña se almacena en `/etc/sico-etl/pgpass`, fuera del `.env` y del DSN.
Ejecutar como `root`:

```bash
read -rsp 'Contraseña PostgreSQL de sico_etl: ' ETL_DB_PASSWORD
echo

ETL_DB_PASSWORD_ESCAPED=$(
  printf '%s' "$ETL_DB_PASSWORD" | sed 's/[\\:]/\\&/g'
)

umask 077
printf '127.0.0.1:5432:dap:sico_etl:%s\n' \
  "$ETL_DB_PASSWORD_ESCAPED" \
  > /etc/sico-etl/pgpass

unset ETL_DB_PASSWORD ETL_DB_PASSWORD_ESCAPED
chown sico-etl:sico-etl /etc/sico-etl/pgpass
chmod 0600 /etc/sico-etl/pgpass
```

`ETL_DB_PASSWORD` es el nombre de una variable temporal: no se reemplaza en el
comando. La contraseña real se escribe en el prompt oculto. El escape es necesario
si contiene `:` o `\`, caracteres especiales del formato `pgpass`.

Agregar al archivo de entorno solamente las referencias no secretas:

```bash
printf '%s\n' \
  'PGPASSFILE=/etc/sico-etl/pgpass' \
  'POSTGRES_DSN=postgresql://sico_etl@127.0.0.1:5432/dap' \
  >> /etc/sico-etl/sico-etl.env
```

Antes de repetir este comando en una reparación o actualización, comprobar que no
duplique las variables:

```bash
grep -nE '^(PGPASSFILE|POSTGRES_DSN)=' /etc/sico-etl/sico-etl.env
```

No ejecutar `cat /etc/sico-etl/pgpass`. Verificar solo propietarios y modos:

```bash
ls -l /etc/sico-etl/pgpass /etc/sico-etl/sico-etl.env
```

Valores esperados:

```text
-rw------- sico-etl sico-etl /etc/sico-etl/pgpass
-rw-r----- root     sico-etl /etc/sico-etl/sico-etl.env
```

## 12. Verificar `pgpass` y el driver Python

Probar sin permitir prompt de contraseña:

```bash
runuser -u sico-etl -- env \
  PGPASSFILE=/etc/sico-etl/pgpass \
  psql -h 127.0.0.1 -U sico_etl -d dap -w \
  -c "SELECT current_user, count(*) AS tablas_control FROM information_schema.tables WHERE table_name LIKE 'etl_%';"
```

Debe devolver `sico_etl` y 5 tablas.

Probar con el mismo `psycopg` que usa el ETL:

```bash
runuser -u sico-etl -- env \
  PGPASSFILE=/etc/sico-etl/pgpass \
  POSTGRES_DSN=postgresql://sico_etl@127.0.0.1:5432/dap \
  /opt/sico-etl/.venv/bin/python -c "
import os
import psycopg

with psycopg.connect(os.environ['POSTGRES_DSN']) as conn:
    row = conn.execute(
        '''
        SELECT
            current_user,
            current_database(),
            current_setting('TimeZone'),
            count(*)
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name LIKE 'etl_%'
        '''
    ).fetchone()
    print(row)
"
```

Resultado validado:

```text
('sico_etl', 'dap', 'America/Lima', 5)
```

## 13. Validaciones pendientes antes de registrar `systemd`

No registrar ni iniciar todavía las unidades mientras los mappings continúen
bloqueados.

Si WinBridgeApi vuelve a actualizarse, comprobar primero desde Ubuntu:

```bash
curl --fail --show-error http://127.0.0.1:15000/health
curl --fail --show-error \
  'http://127.0.0.1:15000/api/v1/extract/articles?limit=1'
```

El siguiente comando ya se ejecutó satisfactoriamente. Repetirlo solo si cambia el
código, la consulta, el binario o la configuración. Cargar el entorno dentro del
proceso que ya ejecuta como el usuario de servicio evita heredar variables
innecesarias de `root`:

```bash
runuser -u sico-etl -- /bin/bash -c '
  set -a
  . /etc/sico-etl/sico-etl.env
  set +a
  exec /opt/sico-etl/.venv/bin/sico-etl run --entity all --dry-run
'
```

Los conteos, duplicados y claves están revisados. En 0.1.5 los seis mappings se
habilitan para proyectar los campos existentes sin interpretación comercial
adicional. Las pruebas aisladas se mantienen como mejora posterior no bloqueante.

Para comprobar el estado instalado sin repetir el `dry-run`, ejecutar como
`root`:

```bash
bash /opt/sico-etl/scripts/validate_ubuntu.sh
```

El script solo informa estado de unidades, listeners, `/health`, versión del
paquete y presencia/permisos de la configuración. No inicia ni detiene servicios,
no escribe en PostgreSQL y no muestra secretos.

Después de instalar 0.1.5:

1. registrar `sico-etl.service` y `sico-etl.timer`;
2. iniciar manualmente el servicio sin bloquear y observar `journalctl`;
3. comprobar que finalice satisfactoriamente;
4. habilitar el timer de cinco minutos y observar su primera ejecución.

La unidad de servicio ejecuta escrituras reales, no `--dry-run`.

## 14. Actualizar el ETL

Para instalar un paquete posterior sin recrear el servidor:

1. verificar la suma SHA-256 y listar el ZIP;
2. respaldar los artefactos actuales, excluyendo secretos;
3. extraer los archivos de aplicación sin sobrescribir `.venv` ni `/etc/sico-etl`;
4. instalar el wheel nuevo en el mismo `venv`;
5. normalizar propietarios de los archivos nuevos;
6. ejecutar todas las pruebas como `sico-etl`;
7. verificar la versión instalada.

Comandos de verificación final:

```bash
/opt/sico-etl/.venv/bin/pip show sico-etl | grep -E '^(Name|Version):'
cd /opt/sico-etl
runuser -u sico-etl -- \
  /opt/sico-etl/.venv/bin/python -m unittest discover -s tests -v
```

No reemplazar `/etc/sico-etl/sico-etl.env` ni `/etc/sico-etl/pgpass` con archivos
del paquete.

## 15. Operación, rollback y riesgos

Cuando el servicio esté finalmente habilitado:

```bash
systemctl status sico-etl.timer sico-etl.service --no-pager
journalctl -u sico-etl.service -n 100 --no-pager
```

El rollback operativo inicial consiste en deshabilitar el timer, sin eliminar
datos ni credenciales:

```bash
systemctl disable --now sico-etl.timer
```

Mejoras posteriores no bloqueantes:

- ejecutar la prueba de upsert contra PostgreSQL aislado;
- falta revisar la exposición de PostgreSQL mediante firewall y `pg_hba.conf`;
- `/query` continúa como excepción temporal y debe operar con una cuenta SQL de
  solo lectura, exclusivamente sobre loopback y túnel;
- WinBridgeApi aún se ejecuta como `LocalSystem` y falta confirmar los permisos
  efectivos de solo lectura de su cuenta SQL.

## Referencias

- [`README.md`](README.md): uso y comportamiento del ETL.
- [`../docs/ETL_MAPPINGS.md`](../docs/ETL_MAPPINGS.md): contratos y claves.
- [`../docs/PROCEDURES.md`](../docs/PROCEDURES.md): operación del túnel Linux.
- [`../WinBridgeApi/DEPLOY.md`](../WinBridgeApi/DEPLOY.md): despliegue Windows.
- [`../docs/ROADMAP.md`](../docs/ROADMAP.md): estado y próximos pasos.
