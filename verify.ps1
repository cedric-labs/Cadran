# scripts/verify.ps1
[CmdletBinding()]
param (
    [string]$ProjectKey = "Fluxa",
    [string]$SonarHostUrl = $(if ($env:SONAR_HOST_URL) { $env:SONAR_HOST_URL } else { "http://localhost:9000" }),
    [string]$SonarToken = $env:SONAR_TOKEN
)

$ErrorActionPreference = 'Stop'

if (-not $SonarToken -and (Test-Path "$PSScriptRoot/.sonar-token")) {
    $SonarToken = (Get-Content "$PSScriptRoot/.sonar-token" -Raw).Trim()
}

function Write-Step ($message) {
    Write-Host "`n=== $message ===" -ForegroundColor Cyan
}

function Assert-LastExitCode ($errorMessage) {
    if ($LASTEXITCODE -ne 0) {
        throw "$errorMessage (Exit code: $LASTEXITCODE)"
    }
}

try {
    # 0. System prerequisites verification
    Write-Step "0. Checking prerequisites"
    $defaultJdkBin = "C:\Program Files\Microsoft\jdk-21.0.12.101-hotspot\bin"
    if (-not (Get-Command "java" -ErrorAction SilentlyContinue) -and (Test-Path "$defaultJdkBin\java.exe")) {
        $env:SONAR_JAVA_PATH = "$defaultJdkBin\java.exe"
        $env:PATH = "$defaultJdkBin;$env:PATH"
    }

    if (-not (Get-Command "java" -ErrorAction SilentlyContinue)) {
        throw "Java runtime not found in PATH. A JRE/JDK 17+ is required to execute SonarScanner."
    }

    if ([string]::IsNullOrWhiteSpace($SonarToken)) {
        throw "SonarQube authentication token is missing. Please set `$env:SONAR_TOKEN or pass -SonarToken."
    }

    Remove-Item -Path "./TestResults" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "./publish" -Recurse -Force -ErrorAction SilentlyContinue

    # 1. Restore local .NET tools
    Write-Step "1. Restoring local .NET tools"
    dotnet tool restore --verbosity quiet
    Assert-LastExitCode "Failed to restore local .NET tools."

    # 2. Code style and formatting check
    Write-Step "2. Verifying code style and formatting"
    dotnet format --verify-no-changes --verbosity diagnostic
    Assert-LastExitCode "Code does not comply with standard formatting rules."

    # 3. Initialize SonarScanner analysis
    Write-Step "3. Initializing SonarScanner analysis"
    dotnet sonarscanner begin `
        /k:"$ProjectKey" `
        /d:sonar.host.url="$SonarHostUrl" `
        /d:sonar.token="$SonarToken" `
        /d:sonar.qualitygate.wait=true `
        /d:sonar.cs.roslyn.ignoreIssues=true `
        /d:sonar.cs.opencover.reportsPaths="**/TestResults/**/coverage.opencover.xml" `
        /d:sonar.cs.vstest.reportsPaths="**/TestResults/**/*.trx" `
        /d:sonar.exclusions="**/Migrations/**,**/wwwroot/lib/**,**/wwwroot/css/fonts.css,publish/**,verify.ps1,**/*.ps1" `
        /d:sonar.coverage.exclusions="**/Migrations/**,**/wwwroot/lib/**,publish/**,**/Cadran.Launcher/UI/**,**/Cadran.Launcher/Program.cs,verify.ps1,**/*.ps1" `
        /d:sonar.cpd.exclusions="**/Migrations/**,**/wwwroot/lib/**,**/wwwroot/css/fonts.css,publish/**,verify.ps1,**/*.ps1"
    Assert-LastExitCode "Failed to initialize SonarScanner."

    # 4. Strict compilation with warnings as errors
    Write-Step "4. Strict compilation (Release mode, warnings as errors)"
    dotnet build --configuration Release --no-incremental -warnaserror
    Assert-LastExitCode "Compilation failed or contains compiler warnings."

    # 4b. Download Playwright browsers
    Write-Step "4b. Installing Playwright headless browsers"
    $playwrightScript = "./tests/Ledger.Tests.E2E/bin/Release/net9.0/playwright.ps1"
    if (Test-Path $playwrightScript) {
        & powershell.exe -ExecutionPolicy Bypass -File $playwrightScript install chromium
        Assert-LastExitCode "Failed to install Playwright browser."
    }

    # 5. Execute test suite with code coverage
    Write-Step "5. Executing test suites with code coverage"
    dotnet test --no-build --configuration Release --logger "trx" --collect:"XPlat Code Coverage;Format=opencover" --results-directory ./TestResults -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude="[FileForge*]*,[LocaleForge*]*,[StatementForge*]*,[SqliteForge*]*"
    Assert-LastExitCode "One or more tests failed."

    # 6. Complete analysis and evaluate Quality Gate
    Write-Step "6. Transmitting analysis to SonarQube and evaluating Quality Gate"
    dotnet sonarscanner end /d:sonar.token="$SonarToken"
    Assert-LastExitCode "SonarQube Quality Gate failed or upload failed."

    Write-Host "`n[OK] All verification checks and SonarQube Quality Gate passed successfully." -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "`n[ERROR] Verification pipeline failed: $_" -ForegroundColor Red
    exit 1
}
