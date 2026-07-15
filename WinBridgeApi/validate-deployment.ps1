<#
Uso (PowerShell, en el servidor Windows):
  .\validate-deployment.ps1

Solo lectura: no inicia, detiene ni modifica servicios, archivos o datos.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = 'WinBridgeApi',
    [string]$BaseUrl = 'http://127.0.0.1:5000',
    [string]$PublishDir = 'C:\Apps\WinBridgeApi\publish',
    [int]$Port = 5000
)

$ErrorActionPreference = 'Stop'
$failures = 0

function Report([string]$state, [string]$message) {
    if ($state -eq 'FAIL') { $script:failures++ }
    $color = @{ PASS='Green'; FAIL='Red'; WARN='Yellow'; INFO='Cyan' }[$state]
    Write-Host "[$state] $message" -ForegroundColor $color
}

Report INFO 'Validacion de solo lectura de WinBridgeApi.'
Report INFO ("Equipo={0}; PowerShell={1}" -f $env:COMPUTERNAME, $PSVersionTable.PSVersion)

$service = Get-WmiObject Win32_Service -Filter ("Name='{0}'" -f $ServiceName.Replace("'", "''")) -ErrorAction SilentlyContinue
if ($null -eq $service) {
    Report FAIL "Servicio $ServiceName no registrado."
} else {
    if ($service.State -eq 'Running') { Report PASS "Servicio RUNNING (PID $($service.ProcessId))." }
    else { Report FAIL "Servicio en estado $($service.State)." }

    if ($service.StartMode -eq 'Auto') { Report PASS 'Inicio automatico.' }
    else { Report WARN "Inicio configurado como $($service.StartMode)." }

    Report INFO "Cuenta: $($service.StartName)"
    Report INFO "Binario: $($service.PathName)"
}

try {
    $runtimes = & dotnet --list-runtimes 2>$null
    $net8 = @($runtimes | Where-Object { $_ -match '^Microsoft\.(NETCore|AspNetCore)\.App 8\.' })
    if ($net8.Count -gt 0) { Report PASS ('.NET 8: ' + ($net8 -join '; ')) }
    else { Report FAIL '.NET 8 runtime no encontrado.' }
} catch { Report FAIL 'No se pudo ejecutar dotnet --list-runtimes.' }

$dll = Join-Path $PublishDir 'WinBridgeApi.dll'
if (Test-Path -LiteralPath $dll) {
    $file = Get-Item -LiteralPath $dll
    Report PASS "DLL encontrada; version=$($file.VersionInfo.FileVersion)"
    try { Report INFO ('SHA-256=' + (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant()) }
    catch { Report WARN 'No se pudo calcular SHA-256 de la DLL.' }
} else { Report FAIL "No existe $dll" }

try {
    $listeners = @(& netstat.exe -ano -p tcp | Where-Object { $_ -match ("^\s*TCP\s+(\S+:{0})\s+\S+\s+LISTENING\s+(\d+)\s*$" -f $Port) })
    if ($listeners.Count -eq 0) {
        Report FAIL "No hay listener TCP en el puerto $Port."
    } else {
        $nonLoopback = @($listeners | Where-Object { $_ -notmatch '127\.0\.0\.1:' -and $_ -notmatch '\[::1\]:' })
        if ($nonLoopback.Count -gt 0) { Report FAIL "Puerto $Port expuesto fuera de loopback: $($nonLoopback -join '; ')" }
        else { Report PASS "Puerto $Port escuchando solo en loopback." }
    }
} catch { Report FAIL 'No se pudo inspeccionar netstat.' }

try {
    $health = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/health') -TimeoutSec 10
    if ($health.status -eq 'ok') { Report PASS "/health OK ($($health.timestamp))." }
    else { Report FAIL "/health devolvio status=$($health.status)." }
} catch { Report FAIL ("/health fallo: " + $_.Exception.Message) }

Write-Host "Resumen: FAIL=$failures"
if ($failures -gt 0) { exit 1 }
