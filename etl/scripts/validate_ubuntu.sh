#!/usr/bin/env bash
# Uso (en Ubuntu): sudo bash /opt/sico-etl/scripts/validate_ubuntu.sh
# Solo lectura: no inicia, detiene ni modifica servicios, archivos o datos.
# Opcional: EXPECTED_ETL_VERSION=0.1.5 sudo -E bash ...

set -uo pipefail
failures=0
expected_etl_version="${EXPECTED_ETL_VERSION:-0.1.5}"

report() {
  local state="$1" message="$2" color
  [[ "$state" == "FAIL" ]] && failures=$((failures + 1))
  case "$state" in
    PASS) color='32';; WARN) color='33';; FAIL) color='31';; *) color='36';;
  esac
  printf '\033[%sm[%s] %s\033[0m\n' "$color" "$state" "$message"
}

timer_state() {
  local unit="$1" load active enabled
  load="$(systemctl show "$unit" -p LoadState --value 2>/dev/null || true)"
  active="$(systemctl is-active "$unit" 2>/dev/null || true)"
  enabled="$(systemctl is-enabled "$unit" 2>/dev/null || true)"
  if [[ "$load" == 'not-found' || -z "$load" ]]; then
    report FAIL "$unit no esta registrado."
    return
  fi
  report INFO "$unit: active=$active; enabled=$enabled"
  [[ "$active" == 'active' ]] && report PASS "$unit activo." || report FAIL "$unit no activo."
  [[ "$enabled" == 'enabled' ]] && report PASS "$unit habilitado al arranque." || report FAIL "$unit no esta habilitado al arranque."
}

oneshot_service_state() {
  local unit="$1" load active result
  load="$(systemctl show "$unit" -p LoadState --value 2>/dev/null || true)"
  active="$(systemctl is-active "$unit" 2>/dev/null || true)"
  result="$(systemctl show "$unit" -p Result --value 2>/dev/null || true)"
  if [[ "$load" == 'not-found' || -z "$load" ]]; then
    report FAIL "$unit no esta registrado."
    return
  fi
  report INFO "$unit: active=$active; ultimo resultado=$result"
  [[ "$active" == 'inactive' ]] && report PASS "$unit inactivo entre ejecuciones (esperado para Type=oneshot)." || report INFO "$unit esta en ejecucion."
  [[ "$result" == 'success' ]] && report PASS 'La ultima ejecucion del servicio finalizo correctamente.' || report FAIL 'La ultima ejecucion del servicio no finalizo correctamente; revisar journalctl.'
}

check_port() {
  local port="$1" required="$2" output nonloopback
  output="$(ss -H -lnt "sport = :$port" 2>/dev/null || true)"
  if [[ -z "$output" ]]; then
    [[ "$required" == 'yes' ]] && report FAIL "No hay listener TCP en :$port." || report WARN "No hay listener TCP en :$port."
    return
  fi
  nonloopback="$(printf '%s\n' "$output" | awk '$4 !~ /^127\./ && $4 !~ /^\[::1\]/ {print $4}')"
  [[ -z "$nonloopback" ]] && report PASS "Puerto :$port solo en loopback." || report WARN "Puerto :$port tambien escucha fuera de loopback: $nonloopback"
}

report INFO 'Validacion de solo lectura de tunel y ETL.'
report INFO "Version ETL esperada: $expected_etl_version"
report INFO "Host=$(hostname); $(. /etc/os-release 2>/dev/null; echo "${PRETTY_NAME:-Linux}")"
report INFO "Python=$(python3 --version 2>&1); PostgreSQL=$(psql --version 2>&1)"

timer_state winbridge-tunnel.service
oneshot_service_state sico-etl.service
timer_state sico-etl.timer
check_port 15000 yes
check_port 5432 no

next_timer="$(systemctl show sico-etl.timer -p NextElapseUSecRealtime --value 2>/dev/null || true)"
last_timer="$(systemctl show sico-etl.timer -p LastTriggerUSec --value 2>/dev/null || true)"
[[ -n "$next_timer" && "$next_timer" != 'n/a' ]] && report PASS "Proxima ejecucion del timer: $next_timer" || report WARN 'No se pudo determinar la proxima ejecucion del timer.'
[[ -n "$last_timer" && "$last_timer" != 'n/a' ]] && report PASS "Ultima activacion del timer: $last_timer" || report WARN 'El timer aun no registra una activacion.'

if curl --fail --silent --show-error --max-time 10 http://127.0.0.1:15000/health >/dev/null; then
  report PASS 'Tunel y /health de WinBridgeApi OK.'
else
  report FAIL 'No responde http://127.0.0.1:15000/health.'
fi

venv=/opt/sico-etl/.venv/bin/python
if [[ -x "$venv" ]]; then
  version="$($venv -m pip show sico-etl 2>/dev/null | awk -F': ' '/^Version:/{print $2}')"
  if [[ -z "$version" ]]; then
    report FAIL 'No se encontro el paquete sico-etl en el venv.'
  elif [[ "$version" == "$expected_etl_version" ]]; then
    report PASS "sico-etl instalado: $version"
  else
    report FAIL "sico-etl instalado: $version; se esperaba $expected_etl_version."
  fi
else
  report FAIL "No existe ejecutable Python del venv: $venv"
fi

check_file_permissions() {
  local file="$1" expected="$2" actual
  if [[ ! -e "$file" ]]; then
    report FAIL "$file no existe."
    return
  fi
  actual="$(stat -c '%U:%G %a' "$file")"
  [[ "$actual" == "$expected" ]] && report PASS "$file existe; permisos=$actual" || report FAIL "$file tiene permisos $actual; se esperaba $expected."
}

check_file_permissions /etc/sico-etl/sico-etl.env 'root:sico-etl 640'
check_file_permissions /etc/sico-etl/pgpass 'sico-etl:sico-etl 600'

if [[ -r /etc/sico-etl/sico-etl.env ]]; then
  base_url="$(sed -n 's/^WINBRIDGE_BASE_URL=//p' /etc/sico-etl/sico-etl.env | tail -n1)"
  [[ "$base_url" == 'http://127.0.0.1:15000' ]] && report PASS 'WINBRIDGE_BASE_URL usa el tunel local.' || report WARN 'WINBRIDGE_BASE_URL no coincide con el valor esperado.'
  grep -q '^POSTGRES_DSN=' /etc/sico-etl/sico-etl.env && report PASS 'POSTGRES_DSN configurado.' || report FAIL 'POSTGRES_DSN ausente.'
  grep -q '^PGPASSFILE=' /etc/sico-etl/sico-etl.env && report PASS 'PGPASSFILE configurado.' || report FAIL 'PGPASSFILE ausente.'
else
  report WARN 'No se puede leer sico-etl.env con el usuario actual; se omitio validar variables.'
fi

if [[ "$(id -u)" -eq 0 && -x /opt/sico-etl/.venv/bin/sico-etl ]]; then
  if runuser -u sico-etl -- /bin/bash -c 'set -a; . /etc/sico-etl/sico-etl.env; set +a; /opt/sico-etl/.venv/bin/sico-etl --help >/dev/null' ; then
    report PASS 'CLI del ETL ejecutable como sico-etl.'
  else
    report FAIL 'La CLI del ETL no se pudo ejecutar como sico-etl.'
  fi
else
  report WARN 'Validacion de CLI como sico-etl omitida: ejecutar el script como root.'
fi

if [[ "$(id -u)" -eq 0 && -r /etc/sico-etl/sico-etl.env ]]; then
  if ! latest_run="$(runuser -u sico-etl -- /bin/bash -s 2>/dev/null <<'BASH'
set -a
. /etc/sico-etl/sico-etl.env
set +a
psql "$POSTGRES_DSN" -X -v ON_ERROR_STOP=1 -At -c "
  SELECT status || '|' || mode || '|' || etl_version || '|' ||
         to_char(finished_at AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS UTC')
  FROM etl_runs
  ORDER BY started_at DESC
  LIMIT 1;"
BASH
)"; then
    report FAIL 'No fue posible consultar los metadatos de ejecucion en PostgreSQL.'
  elif [[ -z "$latest_run" ]]; then
    report FAIL 'No existe ninguna ejecucion registrada en etl_runs.'
  elif [[ "$latest_run" == succeeded\|snapshot\|* ]]; then
    report PASS "Ultima sincronizacion exitosa: $latest_run"
  else
    report FAIL "La ultima ejecucion registrada no fue exitosa: $latest_run. Revisar journalctl."
  fi
else
  report WARN 'Evidencia PostgreSQL omitida: ejecutar como root con la configuracion disponible.'
fi

printf 'Resumen: FAIL=%s\n' "$failures"
[[ "$failures" -eq 0 ]]
