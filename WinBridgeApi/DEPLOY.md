# Despliegue en Windows Server 2012

## Estado desplegado el 2026-07-14

- servicio `WinBridgeApi` registrado, automático y `RUNNING`;
- ejecutable efectivo:
  `C:\Apps\WinBridgeApi\publish\WinBridgeApi.exe`;
- `GET http://127.0.0.1:5000/health` responde `status=ok`;
- los seis endpoints recorrieron snapshots completos desde Ubuntu por el túnel;
- artículos devuelve 14 284 filas después de omitir 15 filas de 6 grupos de
  `ArtCod` duplicado;
- clientes devuelve 6 256 filas después de omitir 35 RUC vacíos y 36 filas de 18
  grupos de RUC duplicado; `ing_cli` nulo usa
  `DATETIMEFROMPARTS(2000,1,1,8,0,0,0)`;
- artefacto fuente efectivo:
  `D:\Desarrollo\extract\WinBridgeApi\publish\WinBridgeApi-customer-duplicate-filter-dll-20260714-154408.zip`;
  se transfirió como
  `C:\Apps\WinBridgeApi-customer-duplicate-filter-dll-20260714-154408.zip`,
  SHA-256
  `d12ed5822c739841cf683490baa38d2b09de5cb0b1d5ce7b2ecc1313b87c3590`;
- DLL desplegada SHA-256
  `969b19c994ed964ed3996aaac24e5e6623375268df74b5430fb11fceefe399db`.

No volver a instalar el servicio ni redesplegar los parches intermedios. Para una
actualización posterior, partir del binario efectivo anterior, conservar
`appsettings.json` y usar el procedimiento de la sección 8.

Para comprobar el estado sin modificar el servidor, copiar y ejecutar
`validate-deployment.ps1` en PowerShell:

```powershell
.\validate-deployment.ps1
```

El script informa servicio, runtime .NET, versión y hash de la DLL, listener
loopback y `/health`. No inicia ni detiene servicios y no consulta SQL.

## 0. Antes de copiar nada

Edita `appsettings.json` (y `appsettings.Development.json` si lo vas a usar)
y reemplaza los placeholders de la cadena de conexión:

```
[PLACEHOLDER_DATABASE], [PLACEHOLDER_USER], [PLACEHOLDER_PASSWORD]
```

## 1. Prerrequisitos en el servidor

- .NET 8 ya instalado (según contexto). Verificar:
  ```
  dotnet --info
  ```
  Debe listar `Microsoft.NETCore.App 8.x` y `Microsoft.AspNetCore.App 8.x`
  en runtimes instalados. Si falta el ASP.NET Core Runtime, instalar el
  "ASP.NET Core Runtime 8.0.x - Windows Hosting Bundle" (o al menos el
  runtime, no hace falta el SDK completo si vas a copiar binarios ya publicados).
- Acceso a `SERVIDOR\SQLSERVERDAP` en el puerto 1998 desde el propio servidor
  (la app se conecta a `localhost,1998`).

## 2. Copiar los archivos

Copia toda la carpeta `WinBridgeApi` (sin `bin/` ni `obj/` si existieran) a
`C:\Apps\WinBridgeApi` en el servidor.

## 3. Prueba rápida en modo interactivo (con SDK)

Si el servidor tiene el SDK de .NET 8 (no solo el runtime), puedes probar
directamente:

```
run.bat
```

o manualmente:

```
dotnet run --urls "http://127.0.0.1:5000"
```

Nota: `Program.cs` ya fija Kestrel a loopback (`http://localhost:5000`) mediante
`ConfigureKestrel`, por lo que el flag `--urls` es redundante (queda como
red de seguridad / documentación), la app siempre escuchará ahí.

Verificar desde el propio servidor:

```
curl http://127.0.0.1:5000/health
```

Debe responder `{"status":"ok","timestamp":"..."}`.

## 4. Publicar para producción (recomendado)

`dotnet run` es una herramienta de desarrollo: recompila en cada arranque y
necesita el SDK y el código fuente presentes. Para producción, publica un
build framework-dependent:

```
cd C:\Apps\WinBridgeApi
dotnet publish -c Release -o publish
```

Esto genera `publish\WinBridgeApi.dll` (y `WinBridgeApi.exe`), que es lo que
`install-service.ps1` espera encontrar.

## 5. Registrar como servicio de Windows

Desde una consola de PowerShell **elevada** (Ejecutar como administrador):

```
cd C:\Apps\WinBridgeApi
.\install-service.ps1
```

El script:
- Verifica que se ejecuta como Administrador.
- Crea el servicio `WinBridgeApi` (DisplayName "Windows Bridge API"),
  `start= auto`, ejecutando `publish\WinBridgeApi.exe`.
- La aplicación detecta al Service Control Manager mediante `AddWindowsService`
  y utiliza el ciclo de vida nativo de un servicio Windows.
- Configura reinicio automático ante caídas (3 intentos, 60s de espera).
- Inicia el servicio.

Verificar:

```
sc.exe query WinBridgeApi
curl http://127.0.0.1:5000/health
```

Parámetro opcional si tu layout difiere:

```
.\install-service.ps1 -PublishDir "C:\Apps\WinBridgeApi\publish"
```

## 6. Exposición de red y firewall

La API escucha únicamente en loopback (`127.0.0.1`/`::1`) y no tiene
autenticación propia. El túnel SSH debe reenviar hacia `127.0.0.1:5000` en el
servidor Windows.

No abras el puerto 5000 en el Firewall de Windows. El binding de Kestrel impide
que la API acepte conexiones dirigidas a las interfaces de red del servidor; el
firewall permanece como control complementario.

Verifica desde otro equipo de la red que `IP_DEL_SERVIDOR:5000` no responde y,
desde el servidor Windows, que `http://127.0.0.1:5000/health` sí responde.

## 7. Recordatorio sobre `/query`

Los endpoints `GET/POST /query` ejecutan SQL arbitrario tal cual se recibe y **no
tienen autenticación**. Su conservación durante el piloto fue autorizada el
2026-07-14. Mientras permanezcan activos, la cadena de conexión debe usar una
cuenta SQL sin permisos de escritura ni DDL y la API debe seguir accesible solo
mediante loopback y el túnel. No forman parte del contrato estable del ETL.

## 8. Actualizar una instalación existente

No sobrescribir el `appsettings.json` productivo con el archivo versionado, que
contiene placeholders. Desde PowerShell elevado:

```powershell
cd C:\Apps\WinBridgeApi
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
sc.exe stop WinBridgeApi
Copy-Item publish "backup\publish-$stamp" -Recurse
```

Esperar que el servicio quede `STOPPED` y que el proceso libere los ensamblados
antes de copiar. Para un parche que solo cambia la aplicación, respaldar y
reemplazar `WinBridgeApi.dll`; esto reduce el riesgo de sobrescribir configuración
o archivos del runtime. Verificar siempre el SHA-256 del ZIP y de la DLL extraída.

Copiar los nuevos binarios dentro de `publish`, conservando el archivo de
configuración real. Luego:

```powershell
sc.exe start WinBridgeApi
sc.exe query WinBridgeApi
Invoke-RestMethod http://127.0.0.1:5000/health
```

Validar `/health` y una página pequeña de cada recurso. Repetir un snapshot
completo desde Ubuntu solo cuando cambie código, consulta, binario o configuración.
Si el servicio no inicia o el contrato falla, detenerlo, restaurar el respaldo y
arrancarlo nuevamente. No ejecutar `install-service.ps1` para una actualización
ordinaria; el servicio ya registrado conserva su configuración y políticas de
recuperación.

## Gestión del servicio

```
sc.exe stop WinBridgeApi
sc.exe start WinBridgeApi
sc.exe delete WinBridgeApi
```

Logs: al correr como servicio, revisa el Visor de eventos de Windows, en
`Registros de Windows > Aplicación`, y filtra por el origen `WinBridgeApi`.
Para depurar interactivamente, detén el servicio y ejecuta
`dotnet publish\WinBridgeApi.dll` en una consola para ver también los logs con
timestamp en vivo.
