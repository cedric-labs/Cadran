# scripts/set-public-visibility.ps1
[CmdletBinding()]
param (
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
    "User-Agent" = "Cadran-Visibility-Script"
}

Write-Host "Checking current repository status for $Repo..." -ForegroundColor Cyan
$repoInfo = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo" -Headers $headers
Write-Host "Current visibility: $($repoInfo.visibility), Private: $($repoInfo.private)" -ForegroundColor Yellow

if ($repoInfo.visibility -ne "public") {
    Write-Host "Updating repository visibility to public..." -ForegroundColor Cyan
    $patchPayload = @{ visibility = "public" } | ConvertTo-Json
    $patchBytes = [System.Text.Encoding]::UTF8.GetBytes($patchPayload)
    $updated = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo" -Method Patch -Headers $headers -Body $patchBytes -ContentType "application/json; charset=utf-8"
    Write-Host "Repository is now PUBLIC: $($updated.html_url)" -ForegroundColor Green
} else {
    Write-Host "Repository is already PUBLIC: $($repoInfo.html_url)" -ForegroundColor Green
}
