#requires -Version 5.1
<#
.SYNOPSIS
    Generates the full MSIX / Microsoft Store tile asset set for
    "Pulse - Digital Wellbeing PC" from a single high-resolution master icon.

.DESCRIPTION
    The Store package (Package.appxmanifest) references a set of PNG logo/tile
    assets under ..\Images\. This script produces every required (and optional)
    asset at the correct pixel dimensions from one square master image.

    IMPORTANT — a real master is REQUIRED:
        The largest existing app icon is only 256x256
        (digital-wellbeing-app\Resources\Icons\digital-balance-icon-256.png).
        That is TOO SMALL for the 310x310 LargeTile and the 620px scale-200
        assets. You MUST create a master of AT LEAST 512x512 (ideally 1024x1024,
        transparent background, square) and pass it via -Master before the tiles
        produced here are Store-quality. Upscaling 256px will look soft/blurry
        and may fail Store certification / WACK visual checks.

    Rendering backend (auto-detected, in order):
        1. ImageMagick  ('magick' on PATH)     -> best quality
        2. .NET System.Drawing (GDI+)          -> fallback, Windows only

.PARAMETER Master
    Path to the >=512px square PNG master icon.
    Default: ..\..\digital-wellbeing-app\Resources\Icons\digital-balance-icon-512.png
    (create this file — it does not exist yet).

.PARAMETER OutDir
    Output directory for generated assets. Default: ..\Images (relative to this script).

.EXAMPLE
    pwsh ./generate-store-assets.ps1 -Master ../../pulse-master-1024.png

.NOTES
    Assets & sizes produced (target/base size; MSIX also wants scaled variants):
        Square44x44Logo.png ......... 44   (app list / taskbar; also 16/24/32/48/256 targetsize)
        Square71x71Logo.png ......... 71   (SmallTile)
        Square150x150Logo.png ....... 150  (medium tile)
        Square310x310Logo.png ....... 310  (LargeTile)  -> saved as LargeTile.png
        Wide310x150Logo.png ......... 310x150 (wide tile)
        StoreLogo.png ............... 50   (Store listing / Properties\Logo)
        SplashScreen.png ............ 620x300 (splash)
    For production, generate scale-100/125/150/200/400 variants; this script
    emits scale-100 base sizes plus scale-200 for the primary tiles.
#>
[CmdletBinding()]
param(
    [string]$Master = "$PSScriptRoot\..\..\digital-wellbeing-app\Resources\Icons\digital-balance-icon-1024.png",
    [string]$OutDir = "$PSScriptRoot\..\Images"
)

$ErrorActionPreference = 'Stop'

# base size, output filename
$targets = @(
    @{ w = 44;  h = 44;  name = 'Square44x44Logo.png' }
    @{ w = 71;  h = 71;  name = 'Square71x71Logo.png' }
    @{ w = 150; h = 150; name = 'Square150x150Logo.png' }
    @{ w = 310; h = 310; name = 'LargeTile.png' }          # Square310x310Logo
    @{ w = 310; h = 150; name = 'Wide310x150Logo.png' }
    @{ w = 50;  h = 50;  name = 'StoreLogo.png' }
    @{ w = 620; h = 300; name = 'SplashScreen.png' }
    # scale-200 primary variants (recommended for crisp high-DPI tiles)
    @{ w = 88;  h = 88;  name = 'Square44x44Logo.scale-200.png' }
    @{ w = 300; h = 300; name = 'Square150x150Logo.scale-200.png' }
    # targetsize variants used by the app list / start / taskbar
    @{ w = 16;  h = 16;  name = 'Square44x44Logo.targetsize-16.png' }
    @{ w = 24;  h = 24;  name = 'Square44x44Logo.targetsize-24.png' }
    @{ w = 32;  h = 32;  name = 'Square44x44Logo.targetsize-32.png' }
    @{ w = 48;  h = 48;  name = 'Square44x44Logo.targetsize-48.png' }
    @{ w = 256; h = 256; name = 'Square44x44Logo.targetsize-256.png' }
)

if (-not (Test-Path -LiteralPath $Master)) {
    Write-Warning @"
Master image not found: $Master

No tiles were generated. Create a >=512x512 square, transparent-background PNG
master and re-run, e.g.:

    pwsh $PSCommandPath -Master C:\path\to\pulse-master-1024.png

Until then, Package.appxmanifest references placeholder Images\*.png paths that
do not yet contain Store-quality art.
"@
    exit 1
}

# Verify master is large enough for the biggest asset (310, or 620 for splash width).
$needed = 620

if (-not (Test-Path -LiteralPath $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}
$OutDir = (Resolve-Path -LiteralPath $OutDir).Path
$Master = (Resolve-Path -LiteralPath $Master).Path

$magick = Get-Command magick -ErrorAction SilentlyContinue

function Get-MasterWidth([string]$path) {
    if ($magick) {
        return [int](& magick identify -format '%w' $path)
    }
    Add-Type -AssemblyName System.Drawing
    $img = [System.Drawing.Image]::FromFile($path)
    try { return $img.Width } finally { $img.Dispose() }
}

$masterW = Get-MasterWidth $Master
if ($masterW -lt 512) {
    Write-Warning "Master is only ${masterW}px wide. >=512px is required for Store-quality tiles; output will be upscaled and may fail certification."
}

if ($magick) {
    Write-Host "Using ImageMagick backend." -ForegroundColor Cyan
    foreach ($t in $targets) {
        $dest = Join-Path $OutDir $t.name
        # Fit within box, center on transparent canvas (keeps aspect for wide/splash).
        & magick $Master -resize "$($t.w)x$($t.h)" -background none -gravity center -extent "$($t.w)x$($t.h)" $dest
        Write-Host "  wrote $($t.name)  ($($t.w)x$($t.h))"
    }
}
else {
    Write-Host "ImageMagick not found; using .NET System.Drawing (GDI+) fallback." -ForegroundColor Yellow
    Add-Type -AssemblyName System.Drawing
    $src = [System.Drawing.Image]::FromFile($Master)
    try {
        foreach ($t in $targets) {
            $dest = Join-Path $OutDir $t.name
            $bmp = New-Object System.Drawing.Bitmap($t.w, $t.h)
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            try {
                $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $g.Clear([System.Drawing.Color]::Transparent)

                # Preserve aspect ratio, center inside the target box.
                $scale = [Math]::Min($t.w / $src.Width, $t.h / $src.Height)
                $dw = [int]($src.Width * $scale)
                $dh = [int]($src.Height * $scale)
                $dx = [int](($t.w - $dw) / 2)
                $dy = [int](($t.h - $dh) / 2)
                $g.DrawImage($src, $dx, $dy, $dw, $dh)
                $bmp.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
                Write-Host "  wrote $($t.name)  ($($t.w)x$($t.h))"
            }
            finally {
                $g.Dispose(); $bmp.Dispose()
            }
        }
    }
    finally {
        $src.Dispose()
    }
}

Write-Host "`nDone. Assets written to: $OutDir" -ForegroundColor Green
Write-Host "Review the tiles visually and re-run with a larger master if any look soft." -ForegroundColor Green
