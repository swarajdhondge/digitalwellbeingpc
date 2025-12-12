# Release script for Digital Wellbeing PC
# Usage: .\scripts\release.ps1 -Version 1.0.11

param(
    [Parameter(Mandatory)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

# Paths
$projectPath = "digital-wellbeing-app/digital-wellbeing-app.csproj"
$publishDir = "./publish"
$releasesDir = "./releases"
$appId = "DigitalWellbeingPC"

Write-Host "Building Digital Wellbeing PC v$Version..." -ForegroundColor Cyan

# Clean previous builds
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $releasesDir) { Remove-Item $releasesDir -Recurse -Force }

# Build and publish
Write-Host "Publishing application..." -ForegroundColor Yellow
dotnet publish $projectPath -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Package with Velopack
Write-Host "Creating Velopack package..." -ForegroundColor Yellow
vpk pack -u $appId -v $Version -p $publishDir -o $releasesDir
if ($LASTEXITCODE -ne 0) { throw "Velopack packaging failed" }

# Create GitHub release and upload
Write-Host "Creating GitHub release v$Version..." -ForegroundColor Yellow
gh release create "v$Version" $releasesDir/* --title "v$Version" --generate-notes
if ($LASTEXITCODE -ne 0) { throw "GitHub release creation failed" }

# Cleanup publish folder
Remove-Item $publishDir -Recurse -Force

Write-Host ""
Write-Host "Successfully released v$Version!" -ForegroundColor Green
Write-Host "Users will be notified of the update automatically." -ForegroundColor Gray
