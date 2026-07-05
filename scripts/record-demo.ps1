<#
  record-demo.ps1 — Pulse demo-video pipeline (the video analogue of capture-screenshots.ps1).

  End-to-end:
    1. Build the app in Release.
    2. Seed a believable fixture database (so the tour isn't empty).
    3. Drive the app with FlaUI on a timed tour (Dashboard -> Screen Time ->
       App Usage +Week -> Hearing -> Focus -> Insights -> Settings + theme toggle)
       while FFmpeg (gdigrab) screen-records the "Pulse" window -> demo.mp4.
    4. Transcode demo.mp4 -> demo.gif (palettegen/paletteuse) for the README.

  Usage (recommended: run from VS Code's integrated terminal — see AV note):
      ./scripts/record-demo.ps1
  Options:
      -Configuration Release|Debug   (default: Release)
      -NoBuild                        skip the build, reuse the last one
      -SkipSeed                       don't reseed the fixture DB
      -OutDir <path>                  output dir (default .github/demo)
      -Fps <int>                      capture framerate (default 30)
      -GifWidth <int>                 README gif width in px (default 960)
      -GifFps <int>                   README gif framerate (default 15)
      -NoGif                          produce only the mp4, skip the gif

  This is a LOCAL pre-release step: GitHub's hosted Windows runners can't reliably
  screen-record a GUI window, so run it on this interactive desktop.

  RELEASE FLOW (fully automatic once the files are committed):
    1. Run this script -> .github/demo/demo.mp4 + demo.gif.
    2. Commit those two files, then tag the release (vX.Y.Z) and push.
    3. .github/workflows/release.yml attaches demo.mp4 + demo.gif to the GitHub
       Release. The README <img> and the marketing-site <video> point at
       releases/latest/download/demo.{gif,mp4}, so GitHub's "latest" redirect swaps
       in the new video across the README AND the website the moment the release
       publishes — no push to main, nothing else to touch.

  NOTE — Quick Heal / antivirus:
    Same caveat as capture-screenshots.ps1 — a freshly-built, unsigned exe launched
    from a plain shell can be quarantined. If the window never appears, run this from
    VS Code's integrated terminal, add a folder exclusion, or build in VS Code once
    then re-run with -NoBuild.

  Idempotent: rerunning rebuilds, reseeds, and overwrites demo.mp4 / demo.gif.
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [switch]$SkipSeed,
    [string]$OutDir,
    [int]$Fps = 30,
    [int]$GifWidth = 960,
    [int]$GifFps = 15,
    [switch]$NoGif
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

$appProject    = Join-Path $repoRoot 'digital-wellbeing-app\digital-wellbeing-app.csproj'
$seederProject = Join-Path $repoRoot 'tools\FixtureSeeder\FixtureSeeder.csproj'
$uiTestProject = Join-Path $repoRoot 'UITests\UITests.csproj'

if (-not $OutDir) { $OutDir = Join-Path $repoRoot '.github\demo' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$mp4 = Join-Path $OutDir 'demo.mp4'
$gif = Join-Path $OutDir 'demo.gif'

# --- Resolve ffmpeg -------------------------------------------------------------
$ffmpeg = (Get-Command ffmpeg -ErrorAction SilentlyContinue).Source
if (-not $ffmpeg) {
    $winget = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\ffmpeg.exe'
    if (Test-Path $winget) { $ffmpeg = $winget }
}
if (-not $ffmpeg) { throw 'ffmpeg not found on PATH. Install it (winget install ffmpeg) or add it to PATH.' }
Write-Host "Using ffmpeg: $ffmpeg" -ForegroundColor DarkGray

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
# Writes to %LocalAppData%\Pulse\digital_wellbeing.db and marks FirstRunCompleted=true
# so the Welcome overlay won't block the tour.
if (-not $SkipSeed) {
    Write-Host 'Seeding fixture database...' -ForegroundColor Cyan
    dotnet run --project $seederProject -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Fixture seeding failed.' }
}

# --- 3. Record the tour ---------------------------------------------------------
# The FlaUI DemoTour test reads PULSE_APP_EXE + PULSE_FFMPEG + PULSE_DEMO_OUT and
# controls ffmpeg itself (start after the window is up, stop with "q" when done).
Write-Host 'Recording demo tour (this drives the live app ~40s — do not touch mouse/keyboard)...' -ForegroundColor Cyan
$env:PULSE_APP_EXE  = $exe
$env:PULSE_FFMPEG   = $ffmpeg
$env:PULSE_DEMO_OUT = $mp4
$env:PULSE_DEMO_FPS = "$Fps"
try {
    dotnet test $uiTestProject -c Debug --nologo `
        --filter 'FullyQualifiedName~DemoTour'
    if ($LASTEXITCODE -ne 0) {
        throw 'Demo tour failed. See the Quick Heal note at the top of this script.'
    }
}
finally {
    Remove-Item Env:\PULSE_APP_EXE  -ErrorAction SilentlyContinue
    Remove-Item Env:\PULSE_FFMPEG   -ErrorAction SilentlyContinue
    Remove-Item Env:\PULSE_DEMO_OUT -ErrorAction SilentlyContinue
    Remove-Item Env:\PULSE_DEMO_FPS -ErrorAction SilentlyContinue
}
if (-not (Test-Path $mp4)) { throw "Recording finished but $mp4 was not produced." }
Write-Host "Wrote $mp4 ($([math]::Round((Get-Item $mp4).Length / 1MB, 2)) MB)" -ForegroundColor Green

# --- 4. Transcode mp4 -> gif (README) -------------------------------------------
# Two-pass palettegen/paletteuse for a clean, small gif; downscaled + lower fps.
if (-not $NoGif) {
    Write-Host "Transcoding -> $gif (${GifWidth}px @ ${GifFps}fps)..." -ForegroundColor Cyan
    $filters = "fps=$GifFps,scale=${GifWidth}:-1:flags=lanczos"
    & $ffmpeg -y -hide_banner -loglevel warning -i $mp4 `
        -vf "$filters,palettegen=stats_mode=diff" `
        -update 1 (Join-Path $OutDir 'palette.png')
    if ($LASTEXITCODE -ne 0) { throw 'palettegen failed.' }
    & $ffmpeg -y -hide_banner -loglevel warning -i $mp4 -i (Join-Path $OutDir 'palette.png') `
        -lavfi "$filters [x]; [x][1:v] paletteuse=dither=bayer:bayer_scale=3" `
        $gif
    if ($LASTEXITCODE -ne 0) { throw 'paletteuse failed.' }
    Remove-Item (Join-Path $OutDir 'palette.png') -ErrorAction SilentlyContinue
    Write-Host "Wrote $gif ($([math]::Round((Get-Item $gif).Length / 1MB, 2)) MB)" -ForegroundColor Green
}

# --- 5. Refresh the marketing-site copy ----------------------------------------
# The site hero plays this same-origin (GitHub release assets are octet-stream and won't
# play inline). README uses .github/demo/demo.gif directly. Commit both after recording.
$siteDemo = Join-Path $repoRoot 'pulse\public\demo'
New-Item -ItemType Directory -Force -Path $siteDemo | Out-Null
Copy-Item $mp4 (Join-Path $siteDemo 'demo.mp4') -Force
Write-Host "Copied demo.mp4 -> pulse/public/demo/" -ForegroundColor DarkGray

Write-Host "Done. Demo assets in $OutDir (+ pulse/public/demo/demo.mp4). Commit them." -ForegroundColor Green
