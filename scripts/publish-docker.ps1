# scripts/publish-docker.ps1
[CmdletBinding()]
param (
    [string]$OutputDir = "./publish/Cadran-Docker",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'

function Write-Step ($message) {
    Write-Host "`n=== $message ===" -ForegroundColor Cyan
}

try {
    Write-Step "1. Cleaning output directory ($OutputDir)"
    if (Test-Path $OutputDir) {
        Remove-Item -Path $OutputDir -Recurse -Force
    }

    $appTargetDir = Join-Path $OutputDir "app"
    $dataDir = Join-Path $OutputDir "data"
    $backupsDir = Join-Path $OutputDir "backups"

    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    New-Item -ItemType Directory -Path $appTargetDir -Force | Out-Null
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
    New-Item -ItemType Directory -Path $backupsDir -Force | Out-Null

    Write-Step "2. Publishing Cadran server engine for Docker container"
    $publishArgs = @("publish", "Ledger/Ledger.csproj", "-c", $Configuration, "-o", $appTargetDir)
    
    & dotnet $publishArgs
    if ($LASTEXITCODE -ne 0) { throw "Cadran server publish failed." }

    Write-Step "3. Cleaning debug symbols (.pdb)"
    Get-ChildItem -Path $OutputDir -Filter "*.pdb" -Recurse | Remove-Item -Force

    Write-Host "`n[SUCCESS] Docker deployment prepared successfully in: $OutputDir" -ForegroundColor Green
}
catch {
    Write-Host "`n[ERROR] Docker publish failed: $_" -ForegroundColor Red
    exit 1
}
