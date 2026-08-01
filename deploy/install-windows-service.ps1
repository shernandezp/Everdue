<#
.SYNOPSIS
    Installs (or removes) Everdue as a Windows service.

.DESCRIPTION
    A thin sc.exe wrapper. The executable already calls Host.UseWindowsService(), so nothing extra
    is needed on the .NET side — this only registers it.

    Run from an elevated PowerShell prompt.

.EXAMPLE
    ./deploy/install-windows-service.ps1 -Path 'C:\Everdue\Everdue.Server.exe' -DataDir 'C:\Everdue\data'
    ./deploy/install-windows-service.ps1 -Uninstall
#>
[CmdletBinding(DefaultParameterSetName = 'Install')]
param(
    [Parameter(ParameterSetName = 'Install', Mandatory = $true)]
    [string]$Path,

    [Parameter(ParameterSetName = 'Install')]
    [string]$DataDir,

    [Parameter(ParameterSetName = 'Install')]
    [string]$Url = 'http://localhost:5080',

    [Parameter(ParameterSetName = 'Install')]
    [string]$ServiceName = 'Everdue',

    [Parameter(ParameterSetName = 'Uninstall', Mandatory = $true)]
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'This script must be run from an elevated PowerShell prompt.'
}

if ($Uninstall) {
    Write-Host "Removing service '$ServiceName'…" -ForegroundColor Cyan
    & sc.exe stop $ServiceName | Out-Null
    Start-Sleep -Seconds 2
    & sc.exe delete $ServiceName
    Write-Host 'Removed.' -ForegroundColor Green
    return
}

if (-not (Test-Path $Path)) {
    throw "Executable not found: $Path"
}

$executable = (Resolve-Path $Path).Path
$installDir = Split-Path -Parent $executable

if (-not $DataDir) {
    $DataDir = Join-Path $installDir 'data'
}

if (-not (Test-Path $DataDir)) {
    New-Item -ItemType Directory -Path $DataDir -Force | Out-Null
}

# The service account has no user profile, so both the URLs and the data directory are set as
# machine-level environment variables rather than left to defaults.
[Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', $Url, 'Machine')
[Environment]::SetEnvironmentVariable('DataDir', $DataDir, 'Machine')
[Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Production', 'Machine')

Write-Host "Installing service '$ServiceName' -> $executable" -ForegroundColor Cyan

& sc.exe create $ServiceName binPath= "`"$executable`"" start= auto DisplayName= 'Everdue'
if ($LASTEXITCODE -ne 0) { throw "sc.exe create failed with exit code $LASTEXITCODE." }

& sc.exe description $ServiceName 'Everdue - operational accountability' | Out-Null
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
& sc.exe start $ServiceName

Write-Host ''
Write-Host "Everdue is installed and running on $Url" -ForegroundColor Green
Write-Host "Data directory: $DataDir"
Write-Host ''
Write-Host 'If this is the first run, set Bootstrap:AdminEmail and Bootstrap:AdminPassword in'
Write-Host "$installDir\appsettings.json and restart the service:  sc.exe stop $ServiceName; sc.exe start $ServiceName"
