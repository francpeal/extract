<#
    Registers WinBridgeApi as a Windows service using sc.exe.

    Must be run from an elevated PowerShell prompt (Run as Administrator)
    on the target Windows Server 2012 machine, AFTER the app has been
    published (see deployment instructions).

    The service runs the published DLL via 'dotnet <dll>', not 'dotnet run':
    'dotnet run' is a dev-time command (it rebuilds on every start and needs
    the SDK + project sources present); it is not suitable for unattended
    service hosting.
#>

param(
    [string]$ServiceName = "WinBridgeApi",
    [string]$DisplayName = "Windows Bridge API",
    [string]$PublishDir  = (Join-Path $PSScriptRoot "publish"),
    [string]$DotnetPath  = ""
)

$ErrorActionPreference = "Stop"

# --- Must run elevated ---
$currentIdentity  = [Security.Principal.WindowsIdentity]::GetCurrent()
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
$isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be run from an elevated (Administrator) PowerShell prompt."
    exit 1
}

# --- Resolve dotnet.exe ---
if ([string]::IsNullOrWhiteSpace($DotnetPath)) {
    $dotnetCmd = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($dotnetCmd) {
        $DotnetPath = $dotnetCmd.Source
    } else {
        $DotnetPath = "C:\Program Files\dotnet\dotnet.exe"
    }
}

if (-not (Test-Path $DotnetPath)) {
    Write-Error "dotnet.exe not found at '$DotnetPath'. Install the .NET 8 Runtime/SDK, or pass -DotnetPath explicitly."
    exit 1
}

# --- Resolve published DLL ---
$dllPath = Join-Path $PublishDir "WinBridgeApi.dll"
if (-not (Test-Path $dllPath)) {
    Write-Error "Published DLL not found at '$dllPath'. Run 'dotnet publish -c Release -o `"$PublishDir`"' first."
    exit 1
}

$binPath = '"' + $DotnetPath + '" "' + $dllPath + '"'

# --- Remove any pre-existing service with the same name ---
sc.exe query $ServiceName | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "Service '$ServiceName' already exists. Stopping and removing it first..."
    sc.exe stop $ServiceName | Out-Null
    Start-Sleep -Seconds 2
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}

Write-Host "Creating service '$ServiceName'..."
sc.exe create $ServiceName binPath= $binPath start= auto DisplayName= $DisplayName obj= "LocalSystem"
if ($LASTEXITCODE -ne 0) {
    Write-Error "sc.exe create failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

sc.exe description $ServiceName "ASP.NET Core 8 minimal API bridge to SQL Server (SERVIDOR\SQLSERVERDAP). HTTP only; secured externally via SSH tunnel."

# Auto-restart on crash: 3 attempts, 60s apart, failure counter resets after 1 day.
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null

Write-Host "Starting service '$ServiceName'..."
sc.exe start $ServiceName

Write-Host ""
Write-Host "Done."
Write-Host "Check status : sc.exe query $ServiceName"
Write-Host "Stop service : sc.exe stop $ServiceName"
Write-Host "Remove service: sc.exe delete $ServiceName"
