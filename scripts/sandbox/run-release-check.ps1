<#
  run-release-check.ps1 - run the release checks for one build in Windows Sandbox.

  Needs a Windows 11 Pro machine with Windows Sandbox enabled, and the .NET SDK for the "ui" and "upgrade" checks.
  Windows Sandbox only starts in the signed-in desktop session (see launch.cmd).

  Checks (comma separated, default smoke):
    smoke     install, start, every page, the CLI, one alert click, uninstall
    alerts    all seven alerts through --test-notify, each clicked in the Notification Center
    tracking  screen time counts input, and a clock jump of one day does no harm
    ui        the FlaUI shell tests against the installed app
    msix      the Store package (needs -Msix)
    upgrade   old Setup.exe -> new Setup.exe over it, data kept (needs -Previous)

  Example:
    .\run-release-check.ps1 -Setup C:\builds\Setup.exe -Previous v2.5.0 -Checks smoke,alerts

  Results: <WorkDir>\<run>\out\RESULTS.txt (PASS/FAIL lines), logs and screenshots in the same folder.
  Exit code 0 only when every line is PASS.
#>
[CmdletBinding()]
param(
    [string]$Setup,                                 # Setup.exe of the build to check (not needed for an msix-only run)
    [string]$Previous,                              # release tag of the version before it (for "upgrade"), for example v2.5.0
    [string]$Checks = 'smoke',
    [string]$Msix,                                  # the .msix to check (for "msix")
    [string]$ExpectedVersion,                       # the installed product version must start with this
    [string]$Repo,                                  # owner/name on GitHub, to download -Previous (default: from git remote origin)
    [string]$WorkDir = (Join-Path $env:TEMP 'pulse-release-check'),
    [ValidateSet('auto', 'none')][string]$Launch = 'auto',   # none: only prepare and wait; start launch.cmd yourself
    [int]$MemoryMB = 8192,
    [int]$TimeoutMinutes = 45
)

$ErrorActionPreference = 'Stop'
$sandboxDir = $PSScriptRoot
$repoRoot = Split-Path (Split-Path $sandboxDir)
$known = 'smoke', 'alerts', 'tracking', 'ui', 'msix', 'upgrade'
$list = @($Checks -split '[,\s]+' | Where-Object { $_ })
foreach ($c in $list) { if ($c -notin $known) { throw "Unknown check '$c'. Known: $($known -join ', ')" } }
if ('upgrade' -in $list -and -not $Previous) { throw 'The upgrade check needs -Previous <release tag>.' }
if ('msix' -in $list -and (-not $Msix -or -not (Test-Path $Msix))) { throw 'The msix check needs -Msix <path to the .msix>.' }
$needSetup = @($list | Where-Object { $_ -ne 'msix' }).Count -gt 0
if ($needSetup -and (-not $Setup -or -not (Test-Path $Setup))) { throw "Setup file not found: $Setup" }
if (Get-Process vmmemWindowsSandbox -ErrorAction SilentlyContinue) { throw 'A Windows Sandbox is already running. Wait for it to end.' }

$run = Join-Path $WorkDir (Get-Date -Format 'yyyyMMdd-HHmmss')
$in = Join-Path $run 'in'; $out = Join-Path $run 'out'
New-Item -ItemType Directory -Force (Join-Path $in 'new'), $out | Out-Null
if ($Setup) { Copy-Item $Setup (Join-Path $in 'new\Setup.exe') }

if ($Previous) {
    if (-not $Repo) { $Repo = ((git -C $repoRoot remote get-url origin) -replace '^.*github\.com[:/]', '' -replace '\.git$', '') }
    $url = "https://github.com/$Repo/releases/download/$Previous/DigitalWellbeingPC-win-Setup.exe"
    New-Item -ItemType Directory -Force (Join-Path $in 'prev') | Out-Null
    Write-Host "Downloading the previous release: $url"
    Invoke-WebRequest $url -OutFile (Join-Path $in 'prev\Setup.exe') -UseBasicParsing
}

$msixName = ''; $msixPublisher = ''; $sdkFolder = ''
if ('msix' -in $list) {
    New-Item -ItemType Directory -Force (Join-Path $in 'msix') | Out-Null
    Copy-Item $Msix (Join-Path $in 'msix')
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $Msix))
    try {
        $r = New-Object IO.StreamReader($zip.GetEntry('AppxManifest.xml').Open())
        $id = ([xml]$r.ReadToEnd()).Package.Identity; $msixName = $id.Name; $msixPublisher = $id.Publisher; $r.Close()
    } finally { $zip.Dispose() }
    # signtool comes from the Windows SDK on this machine, mapped read-only into the sandbox.
    $st = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName | Select-Object -Last 1
    if (-not $st) { throw 'The msix check needs signtool.exe from the Windows SDK on this machine.' }
    $sdkFolder = "`n    <MappedFolder><HostFolder>$(Split-Path (Split-Path $st.FullName))</HostFolder><SandboxFolder>C:\Users\WDAGUtilityAccount\Desktop\sdk</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>"
}

$dotnetFolder = ''
if ('ui' -in $list) {
    Write-Host 'Building the UI tests...'
    dotnet build (Join-Path $repoRoot 'UITests\UITests.csproj') -c Release -o (Join-Path $in 'uit') --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'UI test build failed.' }
    $dotnetDir = Split-Path (Get-Command dotnet).Source
    $dotnetFolder = "`n    <MappedFolder><HostFolder>$dotnetDir</HostFolder><SandboxFolder>C:\Users\WDAGUtilityAccount\Desktop\dotnet</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>"
}
if ('upgrade' -in $list) {
    Write-Host 'Publishing the fixture seeder...'
    dotnet publish (Join-Path $repoRoot 'tools\FixtureSeeder\FixtureSeeder.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $in 'fixseeder') --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Fixture seeder publish failed.' }
}

[ordered]@{ Checks = $list; ExpectedVersion = $ExpectedVersion; MsixName = $msixName; MsixPublisher = $msixPublisher } | ConvertTo-Json | Set-Content (Join-Path $in 'job.json') -Encoding UTF8

$wsb = Join-Path $run 'pulse-release-check.wsb'
(Get-Content (Join-Path $sandboxDir 'pulse-sandbox.wsb.template') -Raw).
    Replace('{{MEMORY_MB}}', "$MemoryMB").Replace('{{SCRIPTS}}', $sandboxDir).Replace('{{IN}}', $in).Replace('{{OUT}}', $out).
    Replace('{{DOTNET_FOLDER}}', $dotnetFolder + $sdkFolder) | Set-Content $wsb -Encoding UTF8
$launchCmd = Join-Path $run 'launch.cmd'
"@echo off`r`ncall `"$(Join-Path $sandboxDir 'launch.cmd')`" `"$wsb`"`r`n" | Set-Content $launchCmd -Encoding ASCII

$clock = [Diagnostics.Stopwatch]::StartNew()
Write-Host "Checks: $($list -join ', ')"
Write-Host "Run folder: $run"
if ($Launch -eq 'auto') {
    if ((Get-Process -Id $PID).SessionId -ne 0 -and [Environment]::UserInteractive) {
        Start-Process $launchCmd -WindowStyle Hidden
    } else {
        # Not in the desktop session: ask Task Scheduler to run launch.cmd there.
        $name = 'pulse-release-check-launch'
        $action = New-ScheduledTaskAction -Execute $launchCmd
        $principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive
        Register-ScheduledTask -TaskName $name -Action $action -Principal $principal -Force | Out-Null
        Start-ScheduledTask -TaskName $name
        Start-Sleep 5
        Unregister-ScheduledTask -TaskName $name -Confirm:$false
    }
} else {
    Write-Host "Start this file in the signed-in desktop session: $launchCmd"
}

$done = Join-Path $out 'done.txt'
while (-not (Test-Path $done)) {
    if ($clock.Elapsed.TotalMinutes -gt $TimeoutMinutes) { Write-Warning "No result after $TimeoutMinutes minutes."; break }
    Start-Sleep 10
}
$elapsed = $clock.Elapsed
# The sandbox shuts itself down when the checks end. Give it a minute, then close what is left.
for ($i = 0; $i -lt 12 -and (Get-Process vmmemWindowsSandbox -ErrorAction SilentlyContinue); $i++) { Start-Sleep 5 }
Get-Process WindowsSandboxRemoteSession, WindowsSandboxClient -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$resultFile = Join-Path $out 'RESULTS.txt'
$lines = if (Test-Path $resultFile) { @(Get-Content $resultFile) } else { @() }
$lines | ForEach-Object { Write-Host $_ }
$fails = @($lines | Where-Object { $_ -like 'FAIL *' }).Count
$ok = (Test-Path $done) -and $lines.Count -gt 0 -and $fails -eq 0
Write-Host ("{0}: {1} line(s), {2} FAIL, run time {3:mm\:ss} (start to end, including sandbox start)" -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $lines.Count, $fails, $elapsed)
if (-not $ok) { exit 1 }
