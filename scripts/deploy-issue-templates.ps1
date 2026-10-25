# scripts/deploy-issue-templates.ps1
[CmdletBinding()]
param (
    [switch]$Push = $false
)

$ErrorActionPreference = 'Stop'

function Write-Step ($msg) {
    Write-Host "`n=== $msg ===" -ForegroundColor Cyan
}

function Get-AppBugReport ($appName) {
@"
name: 🐛 Bug Report
description: Create a report to help us improve $appName
title: "[BUG] "
labels: ["bug"]
body:
  - type: markdown
    attributes:
      value: |
        Thanks for taking the time to report an issue! Please check existing issues before submitting.
  - type: input
    id: version
    attributes:
      label: Application Version
      description: What version or git commit are you running? (e.g. v1.1.0)
      placeholder: v1.1.0
    validations:
      required: true
  - type: dropdown
    id: os
    attributes:
      label: Operating System
      description: What operating system are you using?
      options:
        - Windows 11 x64
        - Windows 10 x64
        - Linux x64 (Ubuntu / Debian)
        - Linux x64 (Arch / Fedora / Other)
        - macOS
        - Other (specify below)
    validations:
      required: true
  - type: dropdown
    id: execution_mode
    attributes:
      label: Deployment / Execution Mode
      description: How are you running the application?
      options:
        - Standalone single-file binary
        - Archive deployment (zip / tar.gz)
        - System service (systemd / Windows service)
        - Windows System Tray Supervisor
        - Built from source (dotnet run)
    validations:
      required: false
  - type: textarea
    id: description
    attributes:
      label: Problem Description
      description: A clear and concise description of the bug.
    validations:
      required: true
  - type: textarea
    id: steps
    attributes:
      label: Steps to Reproduce
      description: Steps to reproduce the behavior.
      placeholder: |
        1. Start application with '...'
        2. Perform action '...'
        3. See error
    validations:
      required: true
  - type: textarea
    id: expected
    attributes:
      label: Expected Behavior
      description: What did you expect to happen?
    validations:
      required: true
  - type: textarea
    id: logs
    attributes:
      label: Logs & Diagnostic Output
      description: Please copy/paste relevant console logs, stack traces, or diagnostic output here.
      render: shell
    validations:
      required: false
  - type: checkboxes
    id: checkboxes
    attributes:
      label: Checklist
      options:
        - label: I have searched existing issues to ensure this is not a duplicate.
          required: true
        - label: I am using an official release or the latest commit on main.
          required: true
"@
}

function Get-AppFeatureRequest ($appName) {
@"
name: 💡 Feature Request
description: Suggest an idea, enhancement, or new capability for $appName
title: "[FEATURE] "
labels: ["enhancement"]
body:
  - type: markdown
    attributes:
      value: |
        Thank you for suggesting an improvement to $appName!
  - type: textarea
    id: problem
    attributes:
      label: Problem Statement / Motivation
      description: Is your feature request related to a problem or limitation? Please describe.
      placeholder: I am looking for a way to [...]
    validations:
      required: true
  - type: textarea
    id: solution
    attributes:
      label: Proposed Solution
      description: Describe the solution or behavior you would like added.
    validations:
      required: true
  - type: textarea
    id: alternatives
    attributes:
      label: Alternatives Considered
      description: Describe any alternative solutions, tools, or workarounds you have considered.
    validations:
      required: false
  - type: textarea
    id: additional_context
    attributes:
      label: Additional Context
      description: Add any other context, screenshots, or mockups here.
    validations:
      required: false
"@
}

function Get-AppConfig ($repoSlug) {
@"
blank_issues_enabled: false
contact_links:
  - name: 📖 Documentation & User Guide
    url: https://github.com/curvecoder/$repoSlug/blob/main/docs/user-guide.md
    about: Please check our documentation and user guide before opening an issue.
"@
}

function Get-FrameworkBugReport ($frameworkName) {
@"
name: 🐛 Bug Report
description: Report a bug or unexpected behavior in $frameworkName
title: "[BUG] "
labels: ["bug"]
body:
  - type: markdown
    attributes:
      value: |
        Thanks for taking the time to report an issue in $frameworkName!
  - type: input
    id: package_version
    attributes:
      label: Package Version
      description: What version of the NuGet package are you using? (e.g. 1.0.0)
      placeholder: 1.0.0
    validations:
      required: true
  - type: dropdown
    id: target_framework
    attributes:
      label: Target .NET Runtime
      options:
        - .NET 9.0
        - .NET 8.0
        - .NET Standard 2.0 / 2.1
        - Other (specify below)
    validations:
      required: true
  - type: dropdown
    id: os
    attributes:
      label: Operating System
      options:
        - Windows
        - Linux
        - macOS
        - Cross-platform / All
    validations:
      required: true
  - type: textarea
    id: reproduction
    attributes:
      label: Minimal Reproducible Example
      description: Please provide a minimal code snippet reproducing the problem.
      render: csharp
    validations:
      required: true
  - type: textarea
    id: expected
    attributes:
      label: Expected Behavior
      description: What did you expect to happen?
    validations:
      required: true
  - type: textarea
    id: actual
    attributes:
      label: Actual Behavior & Exception Stack Trace
      description: Full exception details and stack traces if applicable.
      render: shell
    validations:
      required: true
  - type: checkboxes
    id: checkboxes
    attributes:
      label: Checklist
      options:
        - label: I have searched existing issues to ensure this is not a duplicate.
          required: true
"@
}

function Get-FrameworkFeatureRequest ($frameworkName) {
@"
name: 💡 Feature Request / API Proposal
description: Propose an API enhancement, new capability, or performance improvement in $frameworkName
title: "[FEATURE] "
labels: ["enhancement"]
body:
  - type: markdown
    attributes:
      value: |
        Thank you for proposing an enhancement to $frameworkName!
  - type: textarea
    id: motivation
    attributes:
      label: Motivation / Use Case
      description: What problem are you trying to solve or what use case does this enable?
    validations:
      required: true
  - type: textarea
    id: api_proposal
    attributes:
      label: Proposed API / Solution
      description: If applicable, sketch the proposed public API contracts, methods, or interfaces.
      render: csharp
    validations:
      required: true
  - type: textarea
    id: alternatives
    attributes:
      label: Alternatives Considered
      description: Describe any alternative solutions or workarounds.
    validations:
      required: false
"@
}

function Get-FrameworkConfig ($repoSlug) {
@"
blank_issues_enabled: false
contact_links:
  - name: 📖 Framework Specification
    url: https://github.com/curvecoder/$repoSlug/blob/main/FRAMEWORK.md
    about: Read the framework documentation and contracts before filing.
"@
}

function Deploy-Templates ($repoDir, $appName, $repoSlug, [bool]$isApp) {
    Write-Step "Deploying Issue Templates to $appName ($repoDir)"
    $templateDir = Join-Path $repoDir ".github\ISSUE_TEMPLATE"
    if (-not (Test-Path $templateDir)) {
        New-Item -ItemType Directory -Path $templateDir -Force | Out-Null
    }

    $bugFile = Join-Path $templateDir "bug_report.yml"
    $featFile = Join-Path $templateDir "feature_request.yml"
    $configFile = Join-Path $templateDir "config.yml"

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    if ($isApp) {
        [System.IO.File]::WriteAllText($bugFile, (Get-AppBugReport $appName), $utf8NoBom)
        [System.IO.File]::WriteAllText($featFile, (Get-AppFeatureRequest $appName), $utf8NoBom)
        [System.IO.File]::WriteAllText($configFile, (Get-AppConfig $repoSlug), $utf8NoBom)
    } else {
        [System.IO.File]::WriteAllText($bugFile, (Get-FrameworkBugReport $appName), $utf8NoBom)
        [System.IO.File]::WriteAllText($featFile, (Get-FrameworkFeatureRequest $appName), $utf8NoBom)
        [System.IO.File]::WriteAllText($configFile, (Get-FrameworkConfig $repoSlug), $utf8NoBom)
    }

    Write-Host "Templates written: bug_report.yml, feature_request.yml, config.yml" -ForegroundColor Green

    # Special handling for TopologyForge .gitignore if needed
    if ($appName -eq "TopologyForge") {
        $gi = Join-Path $repoDir ".gitignore"
        $content = Get-Content $gi -Raw
        if ($content -notmatch "nupkg/") {
            Add-Content -Path $gi -Value "`n## Packages NuGet locaux`nnupkg/`n*.nupkg`n"
            Write-Host "Updated .gitignore for TopologyForge" -ForegroundColor Yellow
        }
    }

    # Git commit & push
    $status = git -C $repoDir status --porcelain
    if (-not [string]::IsNullOrWhiteSpace($status)) {
        git -C $repoDir add .github/ISSUE_TEMPLATE/
        if ($appName -eq "TopologyForge") { git -C $repoDir add .gitignore }
        git -C $repoDir commit -m "feat(community): add standardized github issue forms (bug report and feature request)"
        Write-Host "Committed changes in $appName." -ForegroundColor Green

        if ($Push) {
            Write-Host "Pushing to origin main for $appName..." -ForegroundColor Cyan
            git -C $repoDir push origin main
            Write-Host "Pushed $appName successfully." -ForegroundColor Green
        }
    } else {
        Write-Host "No changes to commit in $appName." -ForegroundColor Yellow
    }
}

# 1. Applications
Deploy-Templates "D:\Dev\Fluxa" "Cadran" "Cadran" $true
Deploy-Templates "D:\Dev\NetworkScanner" "NetFlowAudit" "NetFlowAudit" $true

# 2. Shared Frameworks
$frameworks = @("FileForge", "LocaleForge", "ProcessForge", "SocketForge", "SqliteForge", "StatementForge", "TopologyForge")
foreach ($fw in $frameworks) {
    Deploy-Templates "D:\Dev\Framework\$fw" $fw $fw $false
}

Write-Step "All Issue Templates deployed successfully!"
