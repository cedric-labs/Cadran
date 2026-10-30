# scripts/package-release.ps1
[CmdletBinding()]
param (
    [string]$Version = "",
    [string]$DistDir = "./dist"
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Version)) {
    $latestTag = git describe --tags --abbrev=0 2>$null
    if ($latestTag -match '^v?(\d+\.\d+\.\d+.*)$') {
        $Version = $Matches[1]
    } else {
        $Version = "1.0.0"
    }
}

function Write-Step ($message) {
    Write-Host "`n=== $message ===" -ForegroundColor Cyan
}

try {
    Write-Step "1. Preparing distribution directory ($DistDir)"
    if (Test-Path $DistDir) {
        Remove-Item -Path $DistDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

    $winPublishDir = "./publish/Cadran-Windows"
    $linuxPublishDir = "./publish/Cadran-Linux"

    Write-Step "2. Building Windows x64 self-contained package"
    & powershell.exe -ExecutionPolicy Bypass -File "./scripts/publish-windows.ps1" -SelfContained -OutputDir $winPublishDir
    if ($LASTEXITCODE -ne 0) { throw "Windows publish failed." }

    Copy-Item "README.md" (Join-Path $winPublishDir "README.md") -Force
    Copy-Item "LICENSE" (Join-Path $winPublishDir "LICENSE") -Force

    $winZip = Join-Path $DistDir "Cadran-$Version-win-x64.zip"
    Write-Host "Creating $winZip ..." -ForegroundColor Yellow
    Compress-Archive -Path "$winPublishDir\*" -DestinationPath $winZip -CompressionLevel Optimal

    Write-Step "3. Building Linux x64 self-contained package"
    if (Test-Path $linuxPublishDir) {
        Remove-Item -Path $linuxPublishDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $linuxPublishDir -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $linuxPublishDir "data") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $linuxPublishDir "backups") -Force | Out-Null

    $linuxPublishArgs = @(
        "publish", "Ledger/Ledger.csproj",
        "-c", "Release",
        "-r", "linux-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-o", $linuxPublishDir
    )
    & dotnet $linuxPublishArgs
    if ($LASTEXITCODE -ne 0) { throw "Linux publish failed." }

    Get-ChildItem -Path $linuxPublishDir -Filter "*.pdb" -Recurse | Remove-Item -Force
    Copy-Item "README.md" (Join-Path $linuxPublishDir "README.md") -Force
    Copy-Item "LICENSE" (Join-Path $linuxPublishDir "LICENSE") -Force

    $linuxTar = Join-Path $DistDir "Cadran-$Version-linux-x64.tar.gz"
    Write-Host "Creating $linuxTar ..." -ForegroundColor Yellow
    tar -czf $linuxTar -C $linuxPublishDir .

    $linuxStandaloneBin = Join-Path $DistDir "Cadran-$Version-linux-x64"
    Copy-Item (Join-Path $linuxPublishDir "Cadran") $linuxStandaloneBin -Force

    Write-Step "4. Calculating SHA256 checksums"
    $checksumFile = Join-Path $DistDir "SHA256SUMS.txt"
    $lines = @()
    Get-ChildItem -Path $DistDir -File | Where-Object { $_.Name -ne "SHA256SUMS.txt" } | ForEach-Object {
        $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $lines += "$hash  $($_.Name)"
    }
    $lines | Set-Content -Path $checksumFile -Encoding UTF8

    Write-Host "`n[SUCCESS] Release packages generated in $DistDir :" -ForegroundColor Green
    Get-ChildItem -Path $DistDir | Select-Object Name, Length | Format-Table -AutoSize
    Write-Host "Checksums:" -ForegroundColor Cyan
    Get-Content $checksumFile
}
catch {
    Write-Host "`n[ERROR] Packaging failed: $_" -ForegroundColor Red
    exit 1
}
