<#
  build-msix.ps1 — build the Microsoft Store MSIX locally (Partner Center upload).

  Mirrors the CI `msix-build` job in .github/workflows/release.yml exactly: publishes a
  self-contained x64 build, stamps the manifest, and packs a .msix with the Windows SDK
  (makepri + makeappx) — NOT the .wapproj (the VS UWP workload the wapproj needs isn't
  reliably present). Produces an UNSIGNED x64 .msix; that's expected — the Microsoft Store
  RE-SIGNS it on submission, and Store-installed apps are trusted (no SmartScreen prompt).

  Usage (run from a normal PowerShell terminal):
      ./scripts/build-msix.ps1                       # version 2.2.2 -> artifacts/store
      ./scripts/build-msix.ps1 -Version 2.2.3
      ./scripts/build-msix.ps1 -OutDir "$env:USERPROFILE\OneDrive\Desktop\Pulse-Store-Package"

  The resulting Pulse-<version>-x64.msix is what you upload in Partner Center. The Store
  version must be HIGHER than the currently-published one (v2.2.1 shipped, so use 2.2.2+).
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '2.2.2',
    [string]$OutDir
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$four = "$Version.0"

if (-not $OutDir) { $OutDir = Join-Path $repoRoot 'artifacts\store' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$project  = Join-Path $repoRoot 'digital-wellbeing-app\digital-wellbeing-app.csproj'
$pkgDir   = Join-Path $repoRoot 'digital-wellbeing-app.Package'
$stage    = Join-Path $repoRoot 'artifacts\msix-stage'
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage | Out-Null

# --- 1. Publish self-contained x64 (matches CI) ---------------------------------
Write-Host "Publishing Pulse $Version (self-contained x64)..." -ForegroundColor Cyan
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:PublishSingleFile=false -o $stage --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

# --- 2. Stage the manifest + images --------------------------------------------
Copy-Item (Join-Path $pkgDir 'Package.appxmanifest') (Join-Path $stage 'AppxManifest.xml') -Force
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'Images') | Out-Null
Copy-Item (Join-Path $pkgDir 'Images\*.png') (Join-Path $stage 'Images') -Force

# Stamp the 4-part version and declare x64 on the identity (same regex as CI).
$mf = Join-Path $stage 'AppxManifest.xml'
$x = Get-Content $mf -Raw
$x = $x -replace 'Version="[\d.]+"\s*/>', ("Version=`"$four`" ProcessorArchitecture=`"x64`" />")
Set-Content $mf $x
Write-Host "Manifest stamped to $four (x64)." -ForegroundColor DarkGray

# --- 3. Locate the Windows SDK tools -------------------------------------------
$sdkRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
if (-not (Test-Path $sdkRoot)) { throw "Windows SDK not found at $sdkRoot. Install the Windows 10/11 SDK." }
$sdk = Get-ChildItem $sdkRoot -Directory |
    Where-Object { $_.Name -match '^10\.0\.' -and (Test-Path (Join-Path $_.FullName 'x64\makeappx.exe')) } |
    Sort-Object Name -Descending | Select-Object -First 1
if (-not $sdk) { throw "No Windows SDK build with x64\makeappx.exe under $sdkRoot." }
$tool = Join-Path $sdk.FullName 'x64'
Write-Host "Using SDK tools: $tool" -ForegroundColor DarkGray

# --- 4. Build resources.pri + pack the .msix -----------------------------------
$priConfig = Join-Path $stage 'priconfig.xml'
& "$tool\makepri.exe" createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o
if ($LASTEXITCODE -ne 0) { throw 'makepri createconfig failed.' }
& "$tool\makepri.exe" new /pr $stage /cf $priConfig /mn $mf /of (Join-Path $stage 'resources.pri') /o
if ($LASTEXITCODE -ne 0) { throw 'makepri new failed.' }
# priconfig isn't a package payload file — drop it before packing.
Remove-Item $priConfig -ErrorAction SilentlyContinue

$msix = Join-Path $OutDir "Pulse-$Version-x64.msix"
& "$tool\makeappx.exe" pack /d $stage /p $msix /o
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $msix)) { throw 'makeappx did not produce a .msix.' }

$mb = [math]::Round((Get-Item $msix).Length / 1MB, 1)
Write-Host ""
Write-Host "MSIX ready: $msix ($mb MB)" -ForegroundColor Green
Write-Host "Upload this file in Partner Center (Store re-signs it on submission)." -ForegroundColor Green
