# Despliegue en Windows Server 2012

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

Copia toda la carpeta `WinBridgeApi` (sin `bin/` ni `obj/` si existieran) a,
por ejemplo, `D:\Apps\WinBridgeApi` en el servidor.

## 3. Prueba rápida en modo interactivo (con SDK)

Si el servidor tiene el SDK de .NET 8 (no solo el runtime), puedes probar
directamente:

```
run.bat
```

o manualmente:

```
dotnet run --urls "http://0.0.0.0:5000"
```

Nota: `Program.cs` ya fija Kestrel a `http://0.0.0.0:5000` mediante
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
cd D:\Apps\WinBridgeApi
dotnet publish -c Release -o publish
```

Esto genera `publish\WinBridgeApi.dll` (y `WinBridgeApi.exe`), que es lo que
`install-service.ps1` espera encontrar.

## 5. Registrar como servicio de Windows

Desde una consola de PowerShell **elevada** (Ejecutar como administrador):

```
cd D:\Apps\WinBridgeApi
.\install-service.ps1
```

El script:
- Verifica que se ejecuta como Administrador.
- Localiza `dotnet.exe`.
- Crea el servicio `WinBridgeApi` (DisplayName "Windows Bridge API"),
  `start= auto`, ejecutando `dotnet.exe publish\WinBridgeApi.dll`.
- Configura reinicio automático ante caídas (3 intentos, 60s de espera).
- Inicia el servicio.

Verificar:

```
sc.exe query WinBridgeApi
curl http://127.0.0.1:5000/health
```

Parámetros opcionales del script si tu layout difiere:

```
.\install-service.ps1 -PublishDir "D:\Apps\WinBridgeApi\publish" -DotnetPath "C:\Program Files\dotnet\dotnet.exe"
```

## 6. Firewall — importante

La API escucha en `0.0.0.0:5000` **sin autenticación**, confiando en que el
único camino de entrada es el túnel SSH. Para que esa suposición sea cierta
en la práctica:

- Restringe el Firewall de Windows para que el puerto 5000/TCP **no** sea
  alcanzable desde la red externa, solo desde `127.0.0.1` (o desde donde
  termine el túnel SSH localmente).
- Si el túnel SSH reenvía a `127.0.0.1:5000` en el propio servidor, no
  necesitas abrir el puerto 5000 a otras interfaces en absoluto — bloquéalo
  explícitamente con una regla de entrada.

Ejemplo de regla restrictiva (ajusta según tu topología real):

```
netsh advfirewall firewall add rule name="WinBridgeApi-block-external" dir=in protocol=TCP localport=5000 action=block remoteip=any
netsh advfirewall firewall add rule name="WinBridgeApi-allow-localhost" dir=in protocol=TCP localport=5000 action=allow remoteip=127.0.0.1
```

## 7. Recordatorio sobre `/query`

Los endpoints `GET/POST /query` ejecutan SQL arbitrario tal cual se reciben
y **no tienen autenticación**. Son solo para pruebas — elimínalos de
`Program.cs` (o protégelos) antes de dejar esto expuesto de forma
permanente, incluso detrás del túnel.

## Gestión del servicio

```
sc.exe stop WinBridgeApi
sc.exe start WinBridgeApi
sc.exe delete WinBridgeApi
```

Logs: al correr como servicio, la salida de consola no se ve interactivamente.
Para depurar, detén el servicio y ejecuta `dotnet publish\WinBridgeApi.dll`
manualmente en una consola para ver los logs con timestamp en vivo.
