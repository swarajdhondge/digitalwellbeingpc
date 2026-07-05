<#
  capture-screenshots.ps1 — Pulse screenshot pipeline (Phase 3).

  End-to-end:
    1. Build the app in Release.
    2. Seed a believable fixture database (so shots aren't empty).
    3. Drive the app with FlaUI and capture every section in Light + Dark
       to .github/screenshots/ (canonical README filenames).
    4. Copy the marketing-site subset into pulse/public/screenshots/.

  Usage (recommended: run from VS Code's integrated terminal — see AV note):
      ./scripts/capture-screenshots.ps1
  Options:
      -Configuration Release|Debug   (default: Release)
      -NoBuild                        skip the build, reuse the last one
      -SkipSeed                       don't reseed the fixture DB
      -ShotsDir <path>                override output dir (default .github/screenshots)

  NOTE — Quick Heal / antivirus:
    A freshly-built, unsigned DigitalWellbeing.exe launched from a plain shell
    can be quarantined by Quick Heal's behaviour monitor, which will make the
    capture step fail (the window never appears). If that happens:
      * run this script from VS Code's integrated terminal (its process tree is
        trusted by the AV), OR
      * add a one-time Quick Heal folder exclusion for this repo / an
        AV-excluded worktree, OR
      * build once in VS Code (F5 / Run Without Debugging) then re-run with
        -NoBuild so nothing freshly-built is launched from the shell.

  This script is idempotent: rerunning it rebuilds, reseeds, and overwrites the
  same PNGs in place.
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [switch]$SkipSeed,
    [string]$ShotsDir
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

$appProject    = Join-Path $repoRoot 'digital-wellbeing-app\digital-wellbeing-app.csproj'
$seederProject = Join-Path $repoRoot 'tools\FixtureSeeder\FixtureSeeder.csproj'
$uiTestProject = Join-Path $repoRoot 'UITests\UITests.csproj'

if (-not $ShotsDir) { $ShotsDir = Join-Path $repoRoot '.github\screenshots' }
New-Item -ItemType Directory -Force -Path $ShotsDir | Out-Null

# --- 1. Build the app -----------------------------------------------------------
if (-not $NoBuild) {
    Write-Host "Building Pulse ($Configuration)..." -ForegroundColor Cyan
    dotnet build $appProject -c $Configuration -v minimal --nologo
    if ($LASTEXITCODE -ne 0) { throw 'App build failed.' }
}

# Locate the freshly-built exe (TFM folder name can vary between SDK versions).
$binDir = Join-Path $repoRoot "digital-wellbeing-app\bin\$Configuration"
$exe = Get-ChildItem -Path $binDir -Recurse -Filter 'DigitalWellbeing.exe' -ErrorAction SilentlyContinue |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $exe) { throw "Could not find DigitalWellbeing.exe under $binDir (build first)." }
Write-Host "Using app: $exe" -ForegroundColor DarkGray

# --- 2. Seed the fixture database ----------------------------------------------
# Writes to %LocalAppData%\Pulse\digital_wellbeing.db (the path the app reads) and
# also marks FirstRunCompleted=true so the Welcome overlay won't block capture.
if (-not $SkipSeed) {
    Write-Host 'Seeding fixture database...' -ForegroundColor Cyan
    dotnet run --project $seederProject -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Fixture seeding failed.' }
}

# --- 3. Capture screenshots -----------------------------------------------------
# The FlaUI ScreenshotCapture test reads PULSE_APP_EXE + PULSE_SHOTS_DIR.
Write-Host 'Capturing screenshots (Light + Dark)...' -ForegroundColor Cyan
$env:PULSE_APP_EXE  = $exe
$env:PULSE_SHOTS_DIR = $ShotsDir
try {
    dotnet test $uiTestProject -c Debug --nologo `
        --filter 'FullyQualifiedName~ScreenshotCapture'
    if ($LASTEXITCODE -ne 0) {
        throw 'Screenshot capture failed. See the Quick Heal note at the top of this script.'
    }
}
finally {
    Remove-Item Env:\PULSE_APP_EXE  -ErrorAction SilentlyContinue
    Remove-Item Env:\PULSE_SHOTS_DIR -ErrorAction SilentlyContinue
}

# --- 4. Copy the marketing-site subset -----------------------------------------
# The Pulse Next.js site only uses a handful of the dark-theme shots.
$siteDir = Join-Path $repoRoot 'pulse\public\screenshots'
New-Item -ItemType Directory -Force -Path $siteDir | Out-Null

$siteSubset = @(
    'dashboard.png',
    'screentime.png',
    'appusage.png',
    'sound.png',
    'focusmode.png',
    'weeklyreport.png'
)
Write-Host "Copying site subset -> $siteDir" -ForegroundColor Cyan
foreach ($name in $siteSubset) {
    $src = Join-Path $ShotsDir $name
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $siteDir $name) -Force
        Write-Host "  + $name" -ForegroundColor DarkGray
    }
    else {
        Write-Host "  ! missing $name (capture may have failed)" -ForegroundColor Yellow
    }
}

Write-Host "Done. Screenshots in $ShotsDir" -ForegroundColor Green
