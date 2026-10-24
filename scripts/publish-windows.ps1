# scripts/publish-windows.ps1
[CmdletBinding()]
param (
    [string]$OutputDir = "./publish/Cadran-Windows",
    [string]$Configuration = "Release",
    [switch]$SelfContained = $false
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
    $dataDir = Join-Path $appTargetDir "data"
    $backupsDir = Join-Path $appTargetDir "backups"
    $tempLauncherDir = Join-Path $OutputDir "temp_launcher"

    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    New-Item -ItemType Directory -Path $appTargetDir -Force | Out-Null
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
    New-Item -ItemType Directory -Path $backupsDir -Force | Out-Null

    Write-Step "2. Publishing Cadran server engine (into app/ subfolder)"
    $publishArgs = @("publish", "Ledger/Ledger.csproj", "-c", $Configuration, "-r", "win-x64", "-o", $appTargetDir)
    if ($SelfContained) {
        $publishArgs += "--self-contained"
        $publishArgs += "-p:PublishSingleFile=true"
        $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
    } else {
        $publishArgs += "--no-self-contained"
    }
    
    & dotnet $publishArgs
    if ($LASTEXITCODE -ne 0) { throw "Cadran server publish failed." }

    Write-Step "3. Publishing Launcher (single executable at root)"
    $launcherPublishArgs = @(
        "publish", "Cadran.Launcher/Cadran.Launcher.csproj",
        "-c", $Configuration,
        "-r", "win-x64",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-o", $tempLauncherDir
    )
    if ($SelfContained) {
        $launcherPublishArgs += "--self-contained"
    } else {
        $launcherPublishArgs += "--no-self-contained"
    }

    & dotnet $launcherPublishArgs
    if ($LASTEXITCODE -ne 0) { throw "Cadran Launcher publish failed." }

    Write-Step "4. Organizing distribution package"
    # Move Cadran.Launcher.exe to root
    Move-Item (Join-Path $tempLauncherDir "Cadran.Launcher.exe") (Join-Path $OutputDir "Cadran.Launcher.exe") -Force
    
    # Copy icon to root if present
    if (Test-Path "Ledger/cadran.ico") {
        Copy-Item "Ledger/cadran.ico" (Join-Path $OutputDir "cadran.ico") -Force
    }

    # Copy launcher localization resources
    if (Test-Path (Join-Path $tempLauncherDir "Resources")) {
        Copy-Item (Join-Path $tempLauncherDir "Resources") (Join-Path $OutputDir "Resources") -Recurse -Force
    } elseif (Test-Path "Cadran.Launcher/Resources") {
        Copy-Item "Cadran.Launcher/Resources" (Join-Path $OutputDir "Resources") -Recurse -Force
    }

    # Clean temporary launcher directory
    Remove-Item $tempLauncherDir -Recurse -Force -ErrorAction SilentlyContinue

    # Clean debug symbols (.pdb) from release package
    Get-ChildItem -Path $OutputDir -Filter "*.pdb" -Recurse | Remove-Item -Force

    Write-Host "`n[SUCCESS] Windows deployment package assembled in: $OutputDir" -ForegroundColor Green
    Write-Host "Generated structure:" -ForegroundColor Yellow
    Get-ChildItem -Path $OutputDir | Select-Object Name, Length, Mode | Format-Table -AutoSize
}
catch {
    Write-Host "`n[ERROR] Publish failed: $_" -ForegroundColor Red
    exit 1
}
