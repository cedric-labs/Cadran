# scripts/publish-github-release.ps1
[CmdletBinding()]
param (
    [string]$Tag = "v1.0.0",
    [string]$DistDir = "./dist",
    [string]$Repo = "curvecoder/Cadran"
)

$ErrorActionPreference = 'Stop'

# 1. Retrieve GitHub Token from git credential helper
$token = $null
$tempCredFile = [System.IO.Path]::GetTempFileName()
try {
    [System.IO.File]::WriteAllText($tempCredFile, "protocol=https`nhost=github.com`n`n", [System.Text.Encoding]::ASCII)
    $credOutput = cmd.exe /c "type `"$tempCredFile`" | git credential fill"
    $tokenLine = ($credOutput -split "`r?`n") | Where-Object { $_ -match "^password=" }
    if ($tokenLine) {
        $token = ($tokenLine -replace "^password=", "").Trim()
    }
}
finally {
    if (Test-Path $tempCredFile) { Remove-Item $tempCredFile -Force }
}

if ([string]::IsNullOrWhiteSpace($token)) {
    throw "Unable to acquire GitHub Token from git credential helper."
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Accept" = "application/vnd.github.v3+json"
    "User-Agent" = "Cadran-Release-Script"
}

Write-Host "Configuring repository metadata (topics)..." -ForegroundColor Cyan

# 2. Update Topics
$topicsBody = @{
    names = @("personal-finance", "budgeting", "local-first", "privacy-first", "sqlite", "dotnet", "csharp", "self-hosted", "ofx-importer", "dashboard")
} | ConvertTo-Json
$topicsBytes = [System.Text.Encoding]::UTF8.GetBytes($topicsBody)
Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/topics" -Method Put -Headers $headers -Body $topicsBytes -ContentType "application/json; charset=utf-8" | Out-Null
Write-Host "Topics configured." -ForegroundColor Green

# 4. Read SHA256SUMS
$checksumsContent = Get-Content (Join-Path $DistDir "SHA256SUMS.txt") -Raw

$releaseMarkdown = @"
# Cadran v1.0.0 - Privacy-First Personal Finance & Wealth Management

We are excited to announce the initial open-source release of **Cadran v1.0.0**!

Cadran is a modern, local-first, privacy-first personal finance, budget tracking, and wealth management platform built on ASP.NET Core 9, C# 12, Entity Framework Core, and Encrypted SQLite (SQLCipher).

### Key Features
- **Local-First & Privacy-First**: 100% data sovereignty. All records stored locally in encrypted SQLite (`cadran.db`). Zero telemetry, zero external scrapers.
- **Statement Import & Deduplication**: Fast streaming import for OFX 1.x/2.x, QFX, and flexible delimited CSV files with automated fuzzy duplicate detection.
- **Smart Rules & Imputation Engine**: Priority rules matching via wildcards or regex, automated category and payee assignments, and budget imputation date shifts (+1 month).
- **Safe-to-Spend**: Dynamic subscription tracking and pending recurring expense reservation from day 1 of the monthly cycle.
- **Dynamic Pivot Tables (TCD)**: Multi-dimensional aggregations by category, payee, or account with interactive drill-down transaction inspection and CSV export.
- **Windows System Tray Supervisor**: Native notification area launcher (`Cadran.Launcher.exe`) with automated database backups upon start/exit and one-click browser launch.
- **Security & Remote Access**: Local-first architecture with optional SQLCipher AES-256 database encryption. Remote access should be performed exclusively via private VPN (e.g., WireGuard); TOTP 2FA is untested and scheduled for deprecation.
- **Multi-Platform Support**: Self-contained standalone builds for Windows x64 and Linux x64, with systemd and Windows service support.

### Download Artifacts

| Platform / Architecture | Artifact | Description |
|---|---|---|
| **Windows x64** | `Cadran-1.0.0-win-x64.zip` | Complete Windows deployment archive including Tray Supervisor (`Cadran.Launcher.exe`) and server engine. |
| **Windows x64 (Direct)** | `Cadran-1.0.0-win-x64.exe` | Standalone Windows Tray Supervisor executable. |
| **Linux x64** | `Cadran-1.0.0-linux-x64.tar.gz` | Complete Linux deployment archive with configuration and data directories. |
| **Linux x64 (Direct)** | `Cadran-1.0.0-linux-x64` | Standalone Linux server binary. |
| **Checksums** | `SHA256SUMS.txt` | SHA256 checksums for cryptographic verification. |

### SHA256 Integrity Checksums
```text
$checksumsContent
```

For detailed instructions, consult the **[User Guide](https://github.com/curvecoder/Cadran/blob/main/docs/user-guide.md)** and **[Architecture Specification](https://github.com/curvecoder/Cadran/blob/main/docs/architecture.md)**.
"@

# 5. Fetch or Create Release
$release = $null
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/tags/$Tag" -Headers $headers
    Write-Host "Found existing release $($release.id). Updating release body..." -ForegroundColor Cyan
    $updatePayload = @{
        name = "Cadran v1.0.0"
        body = $releaseMarkdown
    } | ConvertTo-Json
    $updateBytes = [System.Text.Encoding]::UTF8.GetBytes($updatePayload)
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/$($release.id)" -Method Patch -Headers $headers -Body $updateBytes -ContentType "application/json; charset=utf-8"
}
catch {
    Write-Host "Creating GitHub Release for $Tag ..." -ForegroundColor Cyan
    $releasePayload = @{
        tag_name = $Tag
        name = "Cadran v1.0.0"
        body = $releaseMarkdown
        draft = $false
        prerelease = $false
    } | ConvertTo-Json
    $releaseBytes = [System.Text.Encoding]::UTF8.GetBytes($releasePayload)
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases" -Method Post -Headers $headers -Body $releaseBytes -ContentType "application/json; charset=utf-8"
}

Write-Host "Release ready with ID $($release.id): $($release.html_url)" -ForegroundColor Green

# 6. Upload release assets
$uploadBase = $release.upload_url.Substring(0, $release.upload_url.IndexOf('{'))

$existingAssets = $release.assets
$assets = Get-ChildItem -Path $DistDir -File

foreach ($file in $assets) {
    # Delete existing asset if present
    $match = $existingAssets | Where-Object { $_.name -eq $file.Name }
    if ($match) {
        Write-Host "Removing existing asset $($file.Name) (ID: $($match.id))..." -ForegroundColor DarkYellow
        Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/assets/$($match.id)" -Method Delete -Headers $headers | Out-Null
    }

    Write-Host "Uploading $($file.Name) ($([math]::Round($file.Length / 1MB, 2)) MB)..." -ForegroundColor Yellow
    
    $contentType = switch ($file.Extension) {
        ".zip" { "application/zip" }
        ".gz"  { "application/gzip" }
        ".exe" { "application/vnd.microsoft.portable-executable" }
        ".txt" { "text/plain" }
        default { "application/octet-stream" }
    }
    
    $uploadUri = "$($uploadBase)?name=$($file.Name)"
    $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)
    
    $uploadHeaders = @{
        "Authorization" = "Bearer $token"
        "User-Agent" = "Cadran-Release-Script"
        "Content-Type" = $contentType
    }
    
    $uploaded = Invoke-RestMethod -Uri $uploadUri -Method Post -Headers $uploadHeaders -Body $fileBytes -TimeoutSec 300
    Write-Host "Uploaded $($file.Name) successfully (ID: $($uploaded.id))." -ForegroundColor Green
}

Write-Host "`n[SUCCESS] GitHub Release v1.0.0 published successfully at: $($release.html_url)" -ForegroundColor Green
