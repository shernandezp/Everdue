<#
.SYNOPSIS
    Builds the client and publishes Everdue as a single self-contained executable.

.DESCRIPTION
    Untrimmed on purpose: trimming breaks EF Core, and ~90 MB is the accepted price of the
    install promise — copy one folder (executable, appsettings.json, wwwroot), run it.

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
Write-Host 'It listens on http://localhost:5000 unless ASPNETCORE_URLS says otherwise.'
Write-Host 'Set Bootstrap:AdminEmail / Bootstrap:AdminPassword to choose the first account, or'
Write-Host 'take the generated admin (admin@everdue.local) from the first-run log banner.'
