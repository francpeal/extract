#!/usr/bin/env bash
# Uso (en Ubuntu): sudo bash /opt/sico-etl/scripts/validate_ubuntu.sh
# Solo lectura: no inicia, detiene ni modifica servicios, archivos o datos.

set -uo pipefail
failures=0

report() {
  local state="$1" message="$2" color
  [[ "$state" == "FAIL" ]] && failures=$((failures + 1))
  case "$state" in
    PASS) color='32';; WARN) color='33';; FAIL) color='31';; *) color='36';;
  esac
  printf '\033[%sm[%s] %s\033[0m\n' "$color" "$state" "$message"
}

unit_state() {
  local unit="$1" required="$2" load active enabled
  load="$(systemctl show "$unit" -p LoadState --value 2>/dev/null || true)"
  active="$(systemctl is-active "$unit" 2>/dev/null || true)"
  enabled="$(systemctl is-enabled "$unit" 2>/dev/null || true)"
  if [[ "$load" == 'not-found' || -z "$load" ]]; then
    [[ "$required" == 'yes' ]] && report FAIL "$unit no esta registrado." || report WARN "$unit no esta registrado."
    return
  fi
  report INFO "$unit: active=$active; enabled=$enabled"
  [[ "$active" == 'active' ]] && report PASS "$unit activo." || { [[ "$required" == 'yes' ]] && report FAIL "$unit no activo." || report WARN "$unit no activo."; }
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
report INFO "Host=$(hostname); $(. /etc/os-release 2>/dev/null; echo "${PRETTY_NAME:-Linux}")"
report INFO "Python=$(python3 --version 2>&1); PostgreSQL=$(psql --version 2>&1)"

unit_state winbridge-tunnel.service yes
unit_state sico-etl.service no
unit_state sico-etl.timer no
check_port 15000 yes
check_port 5432 no

if curl --fail --silent --show-error --max-time 10 http://127.0.0.1:15000/health >/dev/null; then
  report PASS 'Tunel y /health de WinBridgeApi OK.'
else
  report FAIL 'No responde http://127.0.0.1:15000/health.'
fi

venv=/opt/sico-etl/.venv/bin/python
if [[ -x "$venv" ]]; then
  version="$($venv -m pip show sico-etl 2>/dev/null | awk -F': ' '/^Version:/{print $2}')"
  [[ -n "$version" ]] && report PASS "sico-etl instalado: $version" || report FAIL 'No se encontro el paquete sico-etl en el venv.'
else
  report FAIL "No existe ejecutable Python del venv: $venv"
fi

for file in /etc/sico-etl/sico-etl.env /etc/sico-etl/pgpass; do
  if [[ -e "$file" ]]; then
    report PASS "$file existe; permisos=$(stat -c '%U:%G %a' "$file")"
  else
    report FAIL "$file no existe."
  fi
done

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

printf 'Resumen: FAIL=%s\n' "$failures"
[[ "$failures" -eq 0 ]]
