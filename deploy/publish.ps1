<#
.SYNOPSIS
    Builds the client and publishes Everdue as a single self-contained executable.

.DESCRIPTION
    Untrimmed on purpose: trimming breaks EF Core, and ~90 MB is the accepted price of the
    install promise — copy one file plus appsettings.json, run it.

.EXAMPLE
    ./deploy/publish.ps1 -Runtime win-x64
    ./deploy/publish.ps1 -Runtime linux-x64 -Output ./publish
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'linux-x64', 'linux-arm64', 'osx-arm64')]
    [string]$Runtime = 'win-x64',

    [string]$Output = './publish',

    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/Server/Everdue.Server.csproj'
$target = Join-Path $repoRoot $Output $Runtime

Write-Host "Publishing Everdue for $Runtime -> $target" -ForegroundColor Cyan

dotnet publish $project `
    --configuration $Configuration `
    --runtime $Runtime `
    --output $target `
    -p:SelfContainedPublish=true `
    -p:BuildClient=true

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host ''
Write-Host 'Done. To run it:' -ForegroundColor Green
Write-Host "  cd $target"
Write-Host '  ./Everdue.Server'
Write-Host ''
Write-Host 'First run needs Bootstrap:AdminEmail and Bootstrap:AdminPassword in appsettings.json'
Write-Host 'or as environment variables (Bootstrap__AdminEmail / Bootstrap__AdminPassword).'
