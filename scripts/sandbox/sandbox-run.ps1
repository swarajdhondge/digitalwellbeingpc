# Runs INSIDE Windows Sandbox. Started by the LogonCommand of the .wsb file that run-release-check.ps1 writes.
# Reads in\job.json, runs the chosen checks, writes out\RESULTS.txt and out\done.txt, then shuts the sandbox down.
# Every check writes lines "PASS <check>: ..." or "FAIL <check>: ..." with Write-Result.

$desk = 'C:\Users\WDAGUtilityAccount\Desktop'
. "$desk\scripts\lib.ps1"
New-Item -ItemType Directory -Force $outRoot | Out-Null
$job = Get-Content "$inRoot\job.json" -Raw | ConvertFrom-Json
Log "job: checks=$($job.Checks -join ',') screen=$($scr.Width)x$($scr.Height) os=$([Environment]::OSVersion.Version)"

# One alert, shown at once with --test-notify, then clicked in the Notification Center. Pulse is on the Settings page
# when the item is clicked, so a click that opens another page is a real change.
function Test-AlertClick([string]$kind, [string]$expect, [scriptblock]$verify, [bool]$exitBeforeClick = $false) {
  $name = "alert-$kind$(if ($exitBeforeClick) { '-exited' })"
  Fresh-State
  $w = Wait-Toast 45 { Start-Pulse $kind -NoWait }
  Shot "$name-toast"
  if ($w -lt 0) { Write-Result 'FAIL' $name 'no toast within 45 s'; return }
  $null = Wait-Pulse-Window 30
  Place-Pulse; $null = Go-Page 'NavSettings'
  Start-Sleep 12                                   # let the banner leave; the item stays in the Notification Center
  if ($exitBeforeClick) { Kill-Pulse }
  Click-NotificationCenter
  if ($exitBeforeClick) { for ($i = 0; $i -lt 40; $i++) { if (Get-Process DigitalWellbeing -ErrorAction SilentlyContinue) { break }; Start-Sleep 1 }; Start-Sleep 8 }
  Shot "$name-after-click"
  Close-NotificationCenter
  $front = Pulse-Front; $title = Pulse-Title
  $ok = (& $verify $front $title)
  Write-Result $(if ($ok) { 'PASS' } else { 'FAIL' }) $name "toast after ${w}s; expected [$expect]; pulseInFront=$front title='$title'"
}

function Check-Smoke {
  $v = Install-Pulse "$inRoot\new\Setup.exe"
  if (-not $v) { Write-Result 'FAIL' 'smoke-install' 'Setup.exe did not install Pulse'; return }
  $want = $job.ExpectedVersion
  $okV = (-not $want) -or ($v -like "$want*")
  Write-Result $(if ($okV) { 'PASS' } else { 'FAIL' }) 'smoke-install' "installed version $v (expected '$want')"

  Fresh-State
  $p = Start-Pulse
  if (-not $p) { Write-Result 'FAIL' 'smoke-start' 'no Pulse window'; return }
  Place-Pulse
  Write-Result 'PASS' 'smoke-start' 'Pulse window up'

  $bad = @()
  foreach ($pg in @(@('NavDashboard', 'Good '), @('NavScreen', 'Screen Time'), @('NavApps', 'App Usage'), @('NavSound', 'Hearing'), @('NavFocus', 'Focus'),
                    @('NavLimits', 'Limits'), @('NavReports', 'Insights'), @('NavSettings', 'Settings'), @('NavHelp', 'Help'))) {
    if (-not (Go-Page $pg[0] $pg[1])) { $bad += "$($pg[0]) did not show '$($pg[1])' (title '$(Pulse-Title)')" }
  }
  Shot 'smoke-pages'
  Write-Result $(if ($bad.Count -eq 0) { 'PASS' } else { 'FAIL' }) 'smoke-pages' "9 pages$(if ($bad) { ': ' + ($bad -join '; ') })"

  $ver = Run-Cli @('--version'); $st = Run-Cli @('status'); $td = Run-Cli @('today')
  $okC = ($ver.Code -eq 0 -and $ver.Text -and $st.Code -eq 0 -and $td.Code -eq 0)
  Set-Content "$script:dir\cli-smoke.txt" @("--version => $($ver.Code)", $ver.Text, "status => $($st.Code)", $st.Text, "today => $($td.Code)", $td.Text)
  Write-Result $(if ($okC) { 'PASS' } else { 'FAIL' }) 'smoke-cli' "--version=$($ver.Code) status=$($st.Code) today=$($td.Code) text='$($ver.Text)'"

  Test-AlertClick 'goal' 'Pulse in front on the Dashboard' { param($front, $title) $front -and $title.StartsWith('Good ') }

  Kill-Pulse
  $u = Join-Path $appDir 'Update.exe'
  # Without --silent the uninstaller waits for a click on a confirmation dialog.
  $pr = Start-Process $u -ArgumentList 'uninstall', '--silent' -PassThru
  if (-not $pr.WaitForExit(120000)) { Shot 'uninstall-hung'; $pr.Kill() }
  Start-Sleep 10
  $gone = -not (Test-Path (Join-Path $appDir 'current'))
  Write-Result $(if ($gone) { 'PASS' } else { 'FAIL' }) 'smoke-uninstall' "Update.exe uninstall --silent exit $($pr.ExitCode); install folder gone=$gone"
}

function Check-Alerts {
  if (-not (Test-Path $appExe)) { $null = Install-Pulse "$inRoot\new\Setup.exe" }
  Test-AlertClick 'goal'     'Dashboard'               { param($f, $t) $f -and $t.StartsWith('Good ') }
  Test-AlertClick 'goal'     'Pulse started, Dashboard' { param($f, $t) $f -and $t.StartsWith('Good ') } $true
  Test-AlertClick 'limit'    'Limits page'             { param($f, $t) $f -and $t -eq 'Limits' }
  Test-AlertClick 'break'    'break overlay'           { param($f, $t) $f -and (Exists-El 'OverlaySnoozeButton') }
  Test-AlertClick 'winddown' 'Settings page'           { param($f, $t) $f -and $t -eq 'Settings' }
  Test-AlertClick 'focus'    'focus warning overlay'   { param($f, $t) $f -and (Exists-El 'BackToWorkButton') }
  Test-AlertClick 'hearing'  'Hearing page'            { param($f, $t) $f -and $t -eq 'Hearing' }
  Test-AlertClick 'hint'     'Pulse window in front'   { param($f, $t) $f }
}

# Screen time in whole minutes from the "Screen Time: 1h 5m" line of 'pulse today'; -1 when the line is missing.
function Get-ScreenMinutes {
  $line = ((Run-Cli @('today')).Text -split "`n" | Where-Object { $_ -match ':\s*(\d+\s*h\s*)?\d+\s*m\b' } | Select-Object -First 1)
  if (-not $line) { return -1 }
  $h = 0; $m = 0
  if ($line -match '(\d+)\s*h') { $h = [int]$Matches[1] }
  if ($line -match '(\d+)\s*m\b') { $m = [int]$Matches[1] }
  return $h * 60 + $m
}
function Keep-Input([int]$seconds) { $t0 = Get-Date; while (((Get-Date) - $t0).TotalSeconds -lt $seconds) { Nudge; Start-Sleep 5 } }

function Check-Tracking {
  if (-not (Test-Path $appExe)) { $null = Install-Pulse "$inRoot\new\Setup.exe" }
  Fresh-State; $null = Start-Pulse; Place-Pulse
  $m0 = Get-ScreenMinutes
  Keep-Input 300
  Start-Sleep 35                                   # the running app saves its live count every 30 s
  $m1 = Get-ScreenMinutes
  $okIn = ($m0 -ge 0 -and $m1 -ge 0 -and ($m1 - $m0) -ge 4 -and ($m1 - $m0) -le 6)
  Write-Result $(if ($okIn) { 'PASS' } else { 'FAIL' }) 'tracking-input' "5 min of input counted $($m1 - $m0) min (before $m0, after $m1; expected 4 to 6)"
  # Idle time below the 5 minute threshold still counts, so wait past the threshold, then check that the count stands still.
  Start-Sleep 360; $m2 = Get-ScreenMinutes
  Start-Sleep 180; $m3 = Get-ScreenMinutes
  $okIdle = ($m2 -ge 0 -and $m3 -eq $m2 -and ($m2 - $m1) -le 5)
  Write-Result $(if ($okIdle) { 'PASS' } else { 'FAIL' }) 'tracking-idle' "after 6 min idle $m2 min (+$($m2 - $m1) over the input run, at most 5 allowed); 3 min later $m3 min (must be equal)"
  Set-Content "$script:dir\tracking-minutes.txt" @("start=$m0", "after input=$m1", "after 6 min idle=$m2", "after 9 min idle=$m3")
  Keep-Input 20
  $held = Jump-Clock ([TimeSpan]::FromHours(26))
  Start-Sleep 20; Nudge
  $day = (Run-Cli @('today')).Text; $week = (Run-Cli @('week')).Text
  Set-Content "$script:dir\tracking-after-jump.txt" @('today', $day, 'week', $week)
  $mJump = Get-ScreenMinutes
  $errors = @(Pulse-Log | Select-String -Pattern ' ERROR ').Count
  Restore-Clock
  $okDay = ($held -and $errors -eq 0 -and (Get-Process DigitalWellbeing -ErrorAction SilentlyContinue) -and $mJump -le 2)
  Write-Result $(if ($okDay) { 'PASS' } else { 'FAIL' }) 'tracking-day-change' "clock jump held=$held, Pulse running, error lines in log=$errors, today after the jump=$mJump min (a new day starts near 0)"
}

function Check-Ui {
  if (-not (Test-Path $appExe)) { $null = Install-Pulse "$inRoot\new\Setup.exe" }
  Fresh-State; Kill-Pulse
  $env:DOTNET_ROOT = "$desk\dotnet"; $env:DOTNET_CLI_HOME = "$env:TEMP\dn"; $env:DOTNET_NOLOGO = '1'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
  $env:DW_APP_EXE = $appExe; $env:DW_SHOT_DIR = "$script:dir\shots"
  $a = @('vstest', "$inRoot\uit\UITests.dll", '/TestCaseFilter:FullyQualifiedName~ShellTests', '/Logger:trx;LogFileName=uitests.trx', "/ResultsDirectory:$script:dir\trx", '/Logger:console;verbosity=normal')
  $p = Start-Process "$desk\dotnet\dotnet.exe" -ArgumentList $a -RedirectStandardOutput "$script:dir\vstest.out" -RedirectStandardError "$script:dir\vstest.err" -PassThru -WindowStyle Minimized
  if (-not $p.WaitForExit(900000)) { Log 'vstest timeout, killing'; $p.Kill() }
  Write-Result $(if ($p.ExitCode -eq 0) { 'PASS' } else { 'FAIL' }) 'ui' "ShellTests, vstest exit $($p.ExitCode)"
}

function Check-Msix {
  $m = Get-ChildItem "$inRoot\msix" -Filter *.msix | Select-Object -First 1
  # The package is unsigned and carries the Store publisher, so -AllowUnsigned is refused (0x80073D2C). Inside this
  # sandbox only: make a self-signed certificate with the manifest's publisher, trust it, and sign a copy.
  try {
    $cert = New-SelfSignedCertificate -Type Custom -Subject $job.MsixPublisher -KeyUsage DigitalSignature -FriendlyName 'pulse-release-check' `
      -CertStoreLocation 'Cert:\CurrentUser\My' -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    $cer = Join-Path $env:TEMP 'pulse-check.cer'; Export-Certificate -Cert $cert -FilePath $cer | Out-Null
    Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
    $signed = Join-Path $env:TEMP 'pulse-signed.msix'; Copy-Item $m.FullName $signed -Force
    $st = Get-ChildItem "$desk\sdk" -Recurse -Filter signtool.exe | Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1
    $o = & $st.FullName sign /fd SHA256 /sha1 $cert.Thumbprint /s My $signed 2>&1; Log "signtool: $($o -join ' | ')"
    if ($LASTEXITCODE -ne 0) { throw "signtool exit $LASTEXITCODE" }
    Add-AppxPackage -Path $signed -ErrorAction Stop
  } catch { Write-Result 'FAIL' 'msix-install' "$_"; return }
  $pk = Get-AppxPackage -Name $job.MsixName
  Write-Result $(if ($pk) { 'PASS' } else { 'FAIL' }) 'msix-install' "package $($pk.PackageFullName) signature=$($pk.SignatureKind)"
  if (-not $pk) { return }
  $appId = (Get-AppxPackageManifest $pk).Package.Applications.Application.Id
  # An MSIX app cannot take the test variable from the script that starts it, so set it for the whole machine.
  [Environment]::SetEnvironmentVariable('PULSE_TEST_MODE', '1', 'Machine'); Start-Sleep 2
  New-Item -ItemType Directory -Force $pdir | Out-Null; Write-Settings @{ FirstRunCompleted = $true }
  $w = Wait-Toast 60 {
    $mgr = New-Object ApplicationActivationManager; $procId = 0
    [void]$mgr.ActivateApplication("$($pk.PackageFamilyName)!$appId", '--test-notify goal', 0, [ref]$procId)
  }
  Shot 'msix-toast'
  $null = Wait-Pulse-Window 30
  Write-Result $(if ($w -ge 0) { 'PASS' } else { 'FAIL' }) 'msix-alert' "packaged Pulse showed the goal alert after ${w}s"
  if ($w -ge 0) {
    Place-Pulse; $null = Go-Page 'NavSettings'; Start-Sleep 12
    Click-NotificationCenter; Shot 'msix-after-click'; Close-NotificationCenter
    $front = Pulse-Front; $title = Pulse-Title
    Write-Result $(if ($front -and $title.StartsWith('Good ')) { 'PASS' } else { 'FAIL' }) 'msix-alert-click' "pulseInFront=$front title='$title'"
  }
  $alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\pulse.exe'
  $o = (& pulse --version 2>&1 | ForEach-Object { "$_" }) -join ' '
  Write-Result $(if ((Test-Path $alias) -and $o) { 'PASS' } else { 'FAIL' }) 'msix-cli' "alias present=$(Test-Path $alias) output='$o'"
  Get-ChildItem "$env:LOCALAPPDATA\Packages\$($pk.PackageFamilyName)" -Recurse -Include *.log, settings.json -ErrorAction SilentlyContinue |
    ForEach-Object { Copy-Item $_.FullName (Join-Path $script:dir ("msix_" + $_.Name)) -Force }
}

function Check-Upgrade {
  $v1 = Install-Pulse "$inRoot\prev\Setup.exe"
  Fresh-State; Kill-Pulse
  $seed = & "$inRoot\fixseeder\FixtureSeeder.exe" 2>&1; Log "fixture seed: $($seed -join ' | ')"
  Write-Settings @{ FirstRunCompleted = $true; Theme = 'Light'; BreakReminder_Enabled = $true; BreakReminder_Interval = 30 }
  $before = (Run-Cli @('apps', '--week')).Text
  $settingsBefore = Get-Content "$pdir\settings.json" -Raw
  $null = Start-Pulse; Place-Pulse
  # Like a user: run the new Setup.exe over the old install while Pulse is running, then click Update.
  Start-Process "$inRoot\new\Setup.exe"
  $btn = $null; $sw = [Diagnostics.Stopwatch]::StartNew()
  while (-not $btn -and $sw.Elapsed.TotalSeconds -lt 40) {
    foreach ($w in [Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children, [Windows.Automation.Condition]::TrueCondition)) {
      try { foreach ($e in $w.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)) { try { if ($e.Current.Name -match '^Update$') { $btn = $e; break } } catch {} } } catch {}
      if ($btn) { break }
    }
    Start-Sleep 1
  }
  Shot 'upgrade-setup-dialog'
  if ($btn) { $r = $btn.Current.BoundingRectangle; Click-At ([int]($r.X + $r.Width / 2)) ([int]($r.Y + $r.Height / 2)) }
  $sw = [Diagnostics.Stopwatch]::StartNew(); $v2 = $null; $up = $false
  while ($sw.Elapsed.TotalSeconds -lt 240) {
    $v2 = (Get-Item $appExe -ErrorAction SilentlyContinue).VersionInfo.ProductVersion
    $up = [bool](Get-Process DigitalWellbeing -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 })
    if ($v2 -and $v2 -ne $v1 -and $up) { break }
    Start-Sleep 3
  }
  if (-not (Get-Process DigitalWellbeing -ErrorAction SilentlyContinue)) { $null = Start-Pulse }
  Start-Sleep 8
  $after = (Run-Cli @('apps', '--week')).Text
  Set-Content "$script:dir\upgrade-cli.txt" @('before', $before, 'after', $after)
  $sameApps = (@($before -split "`n" | Where-Object { $_.Trim() }).Count -eq @($after -split "`n" | Where-Object { $_.Trim() }).Count)
  Write-Result $(if ($v2 -and $v2 -ne $v1) { 'PASS' } else { 'FAIL' }) 'upgrade-version' "$v1 -> $v2, Setup dialog Update button found=$($null -ne $btn), Pulse running=$up"
  $keptSettings = (Get-Content "$pdir\settings.json" -Raw) -match 'Light'
  Write-Result $(if ($keptSettings -and $sameApps) { 'PASS' } else { 'FAIL' }) 'upgrade-data' "settings kept=$keptSettings, same number of apps in 'pulse apps --week'=$sameApps"
}

try {
  Disable-Dnd
  foreach ($c in $job.Checks) {
    Log "===== check $c ====="
    try {
      switch ($c) {
        'smoke'    { Check-Smoke }
        'alerts'   { Check-Alerts }
        'tracking' { Check-Tracking }
        'ui'       { Check-Ui }
        'msix'     { Check-Msix }
        'upgrade'  { Check-Upgrade }
      }
    } catch { Write-Result 'FAIL' $c "error: $_ $($_.ScriptStackTrace)" }
  }
} catch { Log "RUN ERROR: $_ $($_.ScriptStackTrace)" }
Collect-Logs
Set-Content "$outRoot\done.txt" "done $(Get-Date -Format o)"
shutdown /s /t 0
