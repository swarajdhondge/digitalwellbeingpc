<#
  run.ps1 — build & launch the Digital Wellbeing (Pulse) app.

  Usage:
    From VS Code's integrated terminal (recommended) or any PowerShell:
        ./scripts/run.ps1
    …or just double-click  scripts\run.bat

  Options:
    -Configuration Release   (default: Debug)
    -NoBuild                 (skip the build, just launch the last build)

  NOTE — Quick Heal / antivirus:
    Launching a freshly-built unsigned exe from a plain shell can be blocked
    or quarantined by Quick Heal's behaviour monitor. If that happens:
      • run this script from VS Code's integrated terminal (its process tree
        is trusted by the AV), OR
      • add a one-time Quick Heal folder exclusion for this repo, OR
      • run with F5 / "Run Without Debugging" inside VS Code.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Debug',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot 'digital-wellbeing-app\digital-wellbeing-app.csproj'

if (-not (Test-Path $project)) {
    Write-Host "Project not found: $project" -ForegroundColor Red
    exit 1
}

if (-not $NoBuild) {
    Write-Host "Building Digital Wellbeing ($Configuration)…" -ForegroundColor Cyan
    dotnet build $project -c $Configuration -v minimal --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Build failed — not launching.' -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# Find the built apphost (TFM folder name can change between SDK versions)
$binDir = Join-Path $repoRoot "digital-wellbeing-app\bin\$Configuration"
$exe = Get-ChildItem -Path $binDir -Recurse -Filter 'DigitalWellbeing.exe' -ErrorAction SilentlyContinue |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName

if (-not $exe) {
    Write-Host "Could not find DigitalWellbeing.exe under $binDir" -ForegroundColor Red
    exit 1
}

Write-Host "Launching $exe" -ForegroundColor Green
Start-Process -FilePath $exe
Write-Host 'Started. (If nothing appears, see the Quick Heal note at the top of this script.)' -ForegroundColor DarkGray
