# Procedimientos operativos y migración del servidor Linux

## Propósito

Este documento permite reconstruir el acceso a WinBridgeApi cuando se reemplaza
el servidor Linux/Ubuntu, y sirve como guía de operación normal del túnel.

No contiene contraseñas ni claves privadas. Los valores sensibles deben obtenerse
del mecanismo seguro usado por el responsable de infraestructura.

## Arquitectura vigente

```text
Aplicación en Ubuntu
  http://127.0.0.1:15000
           |
           | túnel SSH (-L)
           v
Windows Server 2012
  WinBridgeApi: http://127.0.0.1:5000
           |
           v
SQL Server 2012: localhost,1998
```

Valores operativos confirmados el 2026-07-13:

| Elemento | Valor |
|---|---|
| Servicio Windows | `WinBridgeApi` |
| Carpeta Windows | `C:\Apps\WinBridgeApi` |
| Puerto API Windows | `127.0.0.1:5000` |
| Servicio Ubuntu | `winbridge-tunnel.service` |
| Puerto consumido por la aplicación | `127.0.0.1:15000` |
| Usuario SSH Windows | `Administrador` |
| Clave usada actualmente | `/root/.ssh/windows_server` |
| Puerto SSH público | `22/TCP`, restringido por IP origen |

La IP pública del cliente y la IP autorizada del servidor Ubuntu no se registran
aquí deliberadamente. Deben confirmarse con infraestructura antes de una
migración.

## Condiciones antes de migrar

Recopilar y confirmar:

- IP pública o nombre DNS del acceso SSH del cliente.
- IP pública fija del nuevo servidor Ubuntu.
- Acceso administrativo al router/firewall que protege el puerto 22.
- Acceso administrativo a Windows u otro procedimiento para instalar una nueva
  clave pública.
- Fingerprint de la clave de host SSH del Windows, obtenido de una fuente
  confiable. No aceptar una clave de host nueva sin validarla.
- URL base usada por la aplicación: `http://127.0.0.1:15000`.
- Respaldo de la configuración de la aplicación Linux, sin guardar secretos en Git.

Para minimizar interrupciones, conservar temporalmente el servidor anterior y
autorizar las IP de origen antigua y nueva durante la transición. Retirar la IP
antigua solo después de completar todas las pruebas.

## 1. Preparar el nuevo Ubuntu

Actualizar índices e instalar el cliente OpenSSH si fuera necesario:

```bash
apt-get update
apt-get install -y openssh-client curl
```

Comprobar herramientas:

```bash
ssh -V
curl --version
systemctl --version
```

Estos comandos instalan el cliente que construye el túnel, `curl` para las pruebas
y confirman que el sistema utiliza `systemd`.

## 2. Autorizar la IP de origen nueva

En el router/firewall del cliente, permitir `22/TCP` únicamente desde la IP pública
del nuevo Ubuntu. Durante la migración puede mantenerse también la IP anterior.

Comprobar desde Ubuntu:

```bash
nc -vz IP_PUBLICA_CLIENTE 22
```

Si `nc` no está instalado, intentar directamente la conexión SSH del paso 4.

Un timeout suele indicar bloqueo de red, NAT o una IP origen no autorizada. Un
`Connection refused` indica que se alcanzó el destino, pero no hay un servicio
escuchando en ese puerto.

## 3. Preparar la identidad SSH

### Opción A — Generar una clave nueva (recomendada)

Generar una identidad exclusiva para este túnel:

```bash
install -d -m 700 /root/.ssh
ssh-keygen -t rsa -b 4096 -N '' -f /root/.ssh/windows_server -C "winbridge-tunnel"
chmod 600 /root/.ssh/windows_server
chmod 644 /root/.ssh/windows_server.pub
```

Para un servicio desatendido, la clave no puede requerir interacción al arrancar.
La clave privada debe quedar protegida por permisos de archivo y por el acceso al
servidor. Nunca enviarla por correo, chat ni incluirla en el repositorio.

Instalar el contenido de `windows_server.pub` como clave autorizada para la cuenta
SSH de Windows. En OpenSSH para Windows, las cuentas administradoras normalmente
usan:

```text
C:\ProgramData\ssh\administrators_authorized_keys
```

Mostrar la clave pública en Ubuntu —nunca la privada—:

```bash
cat /root/.ssh/windows_server.pub
```

En PowerShell elevado de Windows, agregar esa línea completa y corregir permisos:

```powershell
$file = "$env:ProgramData\ssh\administrators_authorized_keys"
if (-not (Test-Path $file)) { New-Item -ItemType File -Path $file | Out-Null }
Add-Content -Path $file -Value "PEGAR_AQUI_LA_CLAVE_PUBLICA_COMPLETA"
icacls.exe $file /inheritance:r
icacls.exe $file /grant "*S-1-5-32-544:F" "*S-1-5-18:F"
```

Los SIDs corresponden al grupo local Administradores y a SYSTEM y evitan depender
del idioma instalado en Windows. Antes de agregar, comprobar que la misma clave no
esté ya presente para evitar duplicados.

### Opción B — Transferir la clave existente

Solo si no se puede registrar una clave nueva, transferir
`/root/.ssh/windows_server` desde el servidor anterior mediante un canal cifrado y
controlado. En el nuevo servidor:

```bash
chown root:root /root/.ssh/windows_server
chmod 600 /root/.ssh/windows_server
```

Eliminar cualquier copia temporal usada durante la transferencia. Esta opción
reutiliza una credencial y ofrece menor trazabilidad que generar una clave nueva.

## 4. Validar SSH y la identidad del servidor

Conectarse primero de forma interactiva:

```bash
ssh \
  -i /root/.ssh/windows_server \
  -o IdentitiesOnly=yes \
  Administrador@IP_PUBLICA_CLIENTE
```

En la primera conexión, comparar el fingerprint presentado por SSH con el valor
confirmado por infraestructura. Aceptarlo únicamente si coincide. Esta conexión
crea la entrada necesaria en `/root/.ssh/known_hosts`.

Una vez dentro, se puede verificar el servicio Windows:

```powershell
sc.exe query WinBridgeApi
Invoke-RestMethod http://127.0.0.1:5000/health
```

Salir de Windows con `exit`.

## 5. Probar el forwarding manual

En una consola de Ubuntu:

```bash
ssh \
  -i /root/.ssh/windows_server \
  -NT \
  -L 127.0.0.1:15000:127.0.0.1:5000 \
  -o BatchMode=yes \
  -o IdentitiesOnly=yes \
  -o ExitOnForwardFailure=yes \
  -o ServerAliveInterval=30 \
  -o ServerAliveCountMax=3 \
  Administrador@IP_PUBLICA_CLIENTE
```

La consola permanece ocupada mientras el túnel está activo. En una segunda
consola:

```bash
curl --fail --show-error http://127.0.0.1:15000/health
```

Respuesta esperada:

```json
{"status":"ok","timestamp":"..."}
```

Para una validación técnica controlada mientras exista `/query`:

```bash
curl --fail --show-error \
  -X POST http://127.0.0.1:15000/query \
  -H 'Content-Type: application/json' \
  -d '{"sql":"SELECT @@VERSION AS version","params":{}}'
```

Esta consulta es de solo lectura. No usar `/query` para modificar información del
ERP. Detener el túnel manual con `Ctrl+C` antes de crear el servicio permanente.

## 6. Crear el servicio systemd

Crear `/etc/systemd/system/winbridge-tunnel.service`:

```ini
[Unit]
Description=SSH tunnel to Windows WinBridgeApi
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=root
ExecStart=/usr/bin/ssh -NT -i /root/.ssh/windows_server -L 127.0.0.1:15000:127.0.0.1:5000 -o BatchMode=yes -o IdentitiesOnly=yes -o ExitOnForwardFailure=yes -o ConnectTimeout=15 -o ServerAliveInterval=30 -o ServerAliveCountMax=3 -o StrictHostKeyChecking=yes Administrador@IP_PUBLICA_CLIENTE
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

`-L` crea el forwarding local. `ExitOnForwardFailure` evita considerar saludable
un proceso que no pudo abrir el puerto. Los `ServerAlive*` detectan conexiones
muertas. `Restart=always` hace que systemd reconstruya el túnel.

Activar y arrancar:

```bash
systemctl daemon-reload
systemctl enable --now winbridge-tunnel.service
```

Verificar:

```bash
systemctl status winbridge-tunnel.service --no-pager
ss -lntp | grep ':15000'
curl --fail --show-error http://127.0.0.1:15000/health
```

El socket debe figurar exclusivamente como `127.0.0.1:15000`, nunca como
`0.0.0.0:15000`.

## 7. Configurar y validar la aplicación

Configurar la aplicación consumidora con:

```text
WINBRIDGE_BASE_URL=http://127.0.0.1:15000
```

El nombre exacto de la variable depende de la aplicación. No debe configurarse la
IP pública del Windows como URL de API: el único destino HTTP es el puerto local
del túnel.

Ejecutar una extracción de prueba y comparar una muestra de precios/stock contra
el ERP antes de habilitar la programación automática.

## 8. Corte y retiro del servidor anterior

Cuando el nuevo servidor haya superado las pruebas:

1. Detener los procesos programados en el servidor anterior.
2. Activar la programación en el nuevo servidor.
3. Vigilar al menos una ejecución completa.
4. En el servidor anterior:

   ```bash
   systemctl disable --now winbridge-tunnel.service
   ```

5. Retirar del router/firewall la IP pública del servidor anterior.
6. Si se generó una clave nueva, retirar de Windows la clave pública anterior
   cuando ya no tenga otros usos.
7. Conservar el servidor anterior durante la ventana de rollback acordada.

## 9. Rollback

Si el nuevo servidor falla durante la migración:

1. Detener en él las automatizaciones y el túnel.
2. Restaurar temporalmente la IP anterior en el firewall si fue retirada.
3. Arrancar el túnel y las automatizaciones en el servidor anterior.
4. Validar `/health` y una extracción.
5. Investigar el servidor nuevo sin ejecutar ambos procesos de extracción a la
   vez, salvo que se haya demostrado que son idempotentes.

## Operación cotidiana

```bash
# Estado
systemctl status winbridge-tunnel.service --no-pager

# Reiniciar el túnel
systemctl restart winbridge-tunnel.service

# Logs recientes
journalctl -u winbridge-tunnel.service -n 100 --no-pager

# Logs en vivo
journalctl -u winbridge-tunnel.service -f

# Validación funcional mínima
curl --fail --show-error http://127.0.0.1:15000/health
```

En Windows:

```powershell
sc.exe query WinBridgeApi
sc.exe stop WinBridgeApi
sc.exe start WinBridgeApi
```

Para actualizar o reinstalar WinBridgeApi, seguir
[`WinBridgeApi/DEPLOY.md`](../WinBridgeApi/DEPLOY.md).

## Diagnóstico rápido

| Síntoma | Causa probable | Comprobación |
|---|---|---|
| SSH termina en timeout | IP nueva no autorizada, NAT o puerto bloqueado | Revisar firewall/router y `IP_PUBLICA_CLIENTE:22` |
| `Permission denied (publickey)` | Clave incorrecta, permisos locales o clave pública no instalada | Revisar `chmod 600`, usuario y `authorized_keys` |
| `REMOTE HOST IDENTIFICATION HAS CHANGED` | Cambio legítimo de host o posible suplantación | Validar fingerprint antes de modificar `known_hosts` |
| `Address already in use` | Otro proceso ocupa el puerto 15000 | `ss -lntp | grep ':15000'` |
| `administratively prohibited` | El servidor SSH no permite forwarding | Revisar `AllowTcpForwarding` en Windows OpenSSH |
| Túnel activo pero `/health` falla | WinBridgeApi detenido o destino incorrecto | Probar `/health` directamente en Windows |
| `/health` funciona y `/query` falla | Configuración SQL, permisos o SQL Server | Revisar Visor de eventos y conexión local |
| El servicio reinicia continuamente | Host key, red, clave o puerto local | `journalctl -u winbridge-tunnel.service` |

Nunca resolver un aviso de cambio de host eliminando `known_hosts` sin confirmar
primero el nuevo fingerprint.

## Endurecimiento futuro

El montaje validado utiliza `root` en Ubuntu y `Administrador` en Windows porque
son las identidades actualmente disponibles. Como mejora posterior, crear cuentas
dedicadas sin acceso interactivo general y limitar la clave SSH para que solo pueda
abrir el forwarding necesario. Este cambio requiere una sesión propia, pruebas y
un plan de rollback; no debe improvisarse durante una migración urgente.

## Validación periódica recomendada

- Mensual: revisar estado, reinicios y logs del servicio.
- Después de cambios de red: probar SSH, `/health` y una extracción controlada.
- Después de actualizar WinBridgeApi: validar servicio Windows, túnel y contrato.
- En una ventana de mantenimiento: reiniciar ambos servidores y confirmar que los
  servicios se recuperan sin intervención manual.

## Operación futura del ETL

La base del sincronizador y las plantillas systemd se documentan en
[`etl/README.md`](../etl/README.md). No habilitar `sico-etl.timer` mientras
`docs/ETL_MAPPINGS.md` mantenga claves o mappings pendientes y los contratos
correspondientes tengan `mapping_confirmed=false`.

La secuencia de aceptación es:

1. Aplicar migraciones de control en un entorno PostgreSQL aislado.
2. Ejecutar las seis entidades con `--dry-run`.
3. Comparar muestras contra SICO y revisar métricas de volumen.
4. Habilitar y probar una entidad maestra pequeña.
5. Ejecutar un snapshot completo sin timer.
6. Verificar idempotencia con una segunda ejecución.
7. Activar el timer solamente después de aprobar rollback y monitoreo.
