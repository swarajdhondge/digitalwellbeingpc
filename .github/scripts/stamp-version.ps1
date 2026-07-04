#requires -Version 5.1
<#
.SYNOPSIS
    Stamp a release version (from the git tag) into the source files that carry a
    hard-coded version, so the tag is the single source of truth.

.DESCRIPTION
    Called by .github/workflows/release.yml before building each channel. Updates:
      - digital-wellbeing-app/Properties/AssemblyInfo.cs  (Assembly*Version attributes)
      - digital-wellbeing-app/app.manifest                (assemblyIdentity version)
    The csproj <Version> and the MSIX AppxManifestVersion are injected separately
    via MSBuild properties in the workflow.

.PARAMETER Version
    Three-part version, e.g. "2.2.0" (the tag with the leading 'v' removed).
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version '$Version' must be three-part (e.g. 2.2.0)."
}

$four = "$Version.0"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

$assemblyInfo = Join-Path $repoRoot 'digital-wellbeing-app/Properties/AssemblyInfo.cs'
(Get-Content $assemblyInfo -Raw) `
    -replace 'AssemblyVersion\("[\d.]+"\)',              "AssemblyVersion(""$four"")" `
    -replace 'AssemblyFileVersion\("[\d.]+"\)',          "AssemblyFileVersion(""$four"")" `
    -replace 'AssemblyInformationalVersion\("[\d.]+"\)', "AssemblyInformationalVersion(""$Version"")" |
    Set-Content $assemblyInfo -NoNewline

$manifest = Join-Path $repoRoot 'digital-wellbeing-app/app.manifest'
(Get-Content $manifest -Raw) `
    -replace 'assemblyIdentity version="[\d.]+"', "assemblyIdentity version=""$four""" |
    Set-Content $manifest -NoNewline

Write-Host "Stamped version $Version ($four) into AssemblyInfo.cs and app.manifest."
