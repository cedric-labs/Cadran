# scripts/refresh-doc-screenshots.ps1
# Copie les captures produites par les tests E2E (DocumentationScreenshot*GeneratorTests) vers docs/images/screens.
[CmdletBinding()]
param (
    [string]$SourceDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'cadran-doc-screenshots'),
    [string]$TargetDir = (Join-Path $PSScriptRoot '..\docs\images\screens')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $SourceDir)) {
    throw "Aucune capture dans '$SourceDir' : lancer d'abord les tests E2E (dotnet test tests/Ledger.Tests.E2E --filter DocumentationScreenshot)."
}

New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
$files = Get-ChildItem -Path $SourceDir -Filter '*.png'
$files | Copy-Item -Destination $TargetDir -Force
Write-Host "$($files.Count) capture(s) copiée(s) vers $TargetDir"
