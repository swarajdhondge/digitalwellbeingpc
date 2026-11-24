# Helpers for the release checks. They run INSIDE Windows Sandbox (dot-sourced by sandbox-run.ps1).
# Windows PowerShell 5.1 only: that is what the sandbox has.

$ErrorActionPreference = 'Continue'
$desk    = 'C:\Users\WDAGUtilityAccount\Desktop'
$inRoot  = "$desk\in"        # read-only: installers, UI test build, job.json
$outRoot = "$desk\out"       # read-write: results come back to the host through this folder
$pdir    = Join-Path $env:LOCALAPPDATA 'Pulse'
$appDir  = Join-Path $env:LOCALAPPDATA 'DigitalWellbeingPC'
$appExe  = Join-Path $appDir 'current\DigitalWellbeing.exe'
$cliExe  = Join-Path $appDir 'current\pulse.exe'

Add-Type -AssemblyName System.Drawing, System.Windows.Forms, UIAutomationClient, UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices;
public class W {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool r);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
  // The window of a process that is hidden in the tray has no MainWindowHandle; find it by class and title.
  public static IntPtr FindMain(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => {
      uint p; GetWindowThreadProcessId(h, out p); if (p != pid) return true;
      var c = new System.Text.StringBuilder(256); GetClassName(h, c, 256);
      var t = new System.Text.StringBuilder(256); GetWindowText(h, t, 256);
      if (c.ToString().StartsWith("HwndWrapper") && t.Length > 0) { found = h; return false; }
      return true; }, IntPtr.Zero);
    return found;
  }
}
[ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IApplicationActivationManager {
  int ActivateApplication([In, MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [In, MarshalAs(UnmanagedType.LPWStr)] string arguments, [In] int options, out uint processId);
}
[ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")] public class ApplicationActivationManager { }
"@
[W]::SetProcessDPIAware() | Out-Null
$scr = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$script:dir = $outRoot
$script:jump = [TimeSpan]::Zero

# ---------------------------------------------------------------- log, results, screenshots
function Log($m) {
  $l = "[{0:HH:mm:ss.fff}] {1}" -f (Get-Date), $m
  Add-Content -Path (Join-Path $script:dir 'events.log') -Value $l
  Add-Content -Path (Join-Path $outRoot 'run.log') -Value $l
}
function Write-Result([string]$state, [string]$check, [string]$text) {
  $line = "{0} {1}: {2}" -f $state, $check, $text
  Add-Content (Join-Path $outRoot 'RESULTS.txt') $line
  Log "RESULT $line"
}
function Shot($name) {
  try {
    $b = New-Object System.Drawing.Bitmap $scr.Width, $scr.Height
    $g = [System.Drawing.Graphics]::FromImage($b); $g.CopyFromScreen($scr.Location, [System.Drawing.Point]::Empty, $scr.Size); $g.Dispose()
    $b.Save((Join-Path $script:dir ("{0:HHmmss}_{1}.png" -f (Get-Date), $name)), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  } catch { Log "shot failed: $_" }
}

# ---------------------------------------------------------------- simulated input
# Pulse counts a PC as in use only while it sees input. Move the mouse a little, now and then.
function Nudge { [W]::SetCursorPos(8, 8) | Out-Null; [W]::mouse_event(1, 3, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60; [W]::mouse_event(1, -3, 0, 0, [UIntPtr]::Zero) }
function Click-At([int]$x, [int]$y) {
  [W]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds 300
  [W]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [W]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
}
function Press-Keys([byte[]]$keys) {
  foreach ($k in $keys) { [W]::keybd_event($k, 0, 0, [UIntPtr]::Zero) }
  $rev = $keys.Clone(); [array]::Reverse($rev)
  foreach ($k in $rev) { [W]::keybd_event($k, 0, 2, [UIntPtr]::Zero) }
}

# ---------------------------------------------------------------- toast detection
# A toast is detected as a change in a small area at the bottom right of the screen.
function Get-Sig {
  $x0 = $scr.Width - 420; $y0 = $scr.Height - 48 - 220; $w = 410; $h = 160
  $b = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($b); $g.CopyFromScreen($x0, $y0, 0, 0, (New-Object System.Drawing.Size $w, $h)); $g.Dispose()
  $sig = New-Object 'System.Collections.Generic.List[int]'
  for ($y = 2; $y -lt $h; $y += 8) { for ($x = 2; $x -lt $w; $x += 10) { $p = $b.GetPixel($x, $y); $sig.Add([int]$p.R + $p.G + $p.B) } }
  $b.Dispose(); return ,$sig.ToArray()
}
function Sig-Diff($a, $b) { $n = 0; for ($i = 0; $i -lt $a.Length; $i++) { if ([Math]::Abs($a[$i] - $b[$i]) -gt 60) { $n++ } }; return $n }
# Returns the seconds until a toast showed, or -1. Sends input every 15 s. $trigger runs after the baseline is taken.
function Wait-Toast([int]$seconds, [scriptblock]$trigger = $null) {
  Start-Sleep 1; $base = Get-Sig; if ($trigger) { $null = & $trigger }
  $sw = [Diagnostics.Stopwatch]::StartNew(); $lastIn = -99
  while ($sw.Elapsed.TotalSeconds -lt $seconds) {
    $t = $sw.Elapsed.TotalSeconds
    if ($t - $lastIn -ge 15) { Nudge; $lastIn = $t }
    if ((Sig-Diff $base (Get-Sig)) -gt 25) { return [int]$t }
    Start-Sleep -Milliseconds 250
  }
  return -1
}

# ---------------------------------------------------------------- standard step: Do Not Disturb and the bell
# Windows turns Do Not Disturb on by itself (for example for an hour after an update) and the sandbox inherits it,
# so toasts never show. Clear it in the registry, restart the shell, then prove it with a probe toast. If the probe
# does not show, click the bell in the Notification Center once (UI Automation cannot see that window) and probe again.
function Show-ProbeToast {
  try {
    [void][Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]
    [void][Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime]
    $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
    $xml.LoadXml('<toast><visual><binding template="ToastText02"><text id="1">Toast probe</text><text id="2">Release check</text></binding></visual></toast>')
    $aumid = '{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe'
    [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($aumid).Show([Windows.UI.Notifications.ToastNotification]::new($xml))
  } catch { Log "probe toast failed: $_" }
}
function Open-NotificationCenter { Press-Keys @(0x5B, 0x4E); Start-Sleep 3 }
function Close-NotificationCenter { Press-Keys @(0x1B); Start-Sleep 1 }
function Disable-Dnd {
  $n = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings'
  New-Item -Path $n -Force | Out-Null
  foreach ($v in 'NOC_GLOBAL_SETTING_TOASTS_ENABLED', 'NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK', 'NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND', 'NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK') {
    Set-ItemProperty -Path $n -Name $v -Value 1 -Type DWord -ErrorAction SilentlyContinue
  }
  $p = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\PushNotifications'
  New-Item -Path $p -Force | Out-Null
  Set-ItemProperty -Path $p -Name 'ToastEnabled' -Value 1 -Type DWord
  Get-Process explorer -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep 8
  Log "DND: registry set, shell restarted (explorer running: $([bool](Get-Process explorer -ErrorAction SilentlyContinue)))"
  $w = Wait-Toast 6 { Show-ProbeToast }
  if ($w -lt 0) {
    Log 'DND: probe toast not seen; clicking the bell'
    Open-NotificationCenter; Shot 'dnd-nc-before'
    Click-At ($scr.Width - 69) ($scr.Height - 574); Start-Sleep 2; Shot 'dnd-nc-after'
    Close-NotificationCenter
    $w = Wait-Toast 6 { Show-ProbeToast }
  }
  Shot 'dnd-final'
  $script:toastsOk = ($w -ge 0)
  Write-Result $(if ($script:toastsOk) { 'PASS' } else { 'FAIL' }) 'toasts' "probe toast visible=$($script:toastsOk)"
  Start-Sleep 8   # let the probe banner leave before the first real toast
}
# Click the newest item in the Notification Center by position (UI Automation cannot see that window).
function Click-NotificationCenter {
  Open-NotificationCenter; Shot 'nc-before-click'
  Click-At ($scr.Width - 290) 235
  Start-Sleep 3
}

# ---------------------------------------------------------------- standard step: clock
# Hyper-V time sync would undo a clock jump within seconds, so stop it first.
function Stop-TimeSync {
  foreach ($sv in 'vmictimesync', 'w32time') {
    try { Stop-Service $sv -Force -ErrorAction Stop; Set-Service $sv -StartupType Disabled -ErrorAction Stop; Log "stopped and disabled $sv" } catch { Log "could not stop ${sv}: $_" }
  }
  try { w32tm /config /syncfromflags:NO /update 2>&1 | ForEach-Object { Log "w32tm: $_" } } catch {}
}
function Jump-Clock([TimeSpan]$by) {
  Stop-TimeSync
  $target = (Get-Date) + $by
  try { Set-Date -Date $target -ErrorAction Stop | Out-Null } catch { Log "Set-Date error: $_" }
  foreach ($w in 3, 7, 20) { Start-Sleep $w }
  $d = ((Get-Date) - $target).TotalSeconds
  $held = ($d -gt -5 -and $d -lt 120)
  if ($held) { $script:jump += $by }
  Log "clock jump +$by held: $held (now $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))"
  return $held
}
function Restore-Clock { if ($script:jump -ne [TimeSpan]::Zero) { Set-Date -Date ((Get-Date) - $script:jump) | Out-Null; $script:jump = [TimeSpan]::Zero } }

# ---------------------------------------------------------------- Pulse: install, start, state
function Kill-Pulse { Get-Process DigitalWellbeing -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep 2 }
function Install-Pulse([string]$setup, [bool]$silent = $true) {
  Log "installing $setup (silent=$silent)"
  if ($silent) { Start-Process $setup -ArgumentList '--silent' -Wait } else { Start-Process $setup }
  for ($i = 0; $i -lt 90; $i++) { if (Test-Path $appExe) { break }; Start-Sleep 2 }
  if (-not (Test-Path $appExe)) { Log 'installed exe not found'; return $null }
  $v = (Get-Item $appExe).VersionInfo.ProductVersion
  Log "installed product version $v"
  return $v
}
function Fresh-State {
  Kill-Pulse
  if (Test-Path $pdir) { Move-Item $pdir "$pdir.before-$(Get-Date -Format HHmmss)" }
  New-Item -ItemType Directory -Force $pdir | Out-Null
  Write-Settings @{ FirstRunCompleted = $true }
}
function Write-Settings($h) { New-Item -ItemType Directory -Force $pdir | Out-Null; ($h | ConvertTo-Json) | Set-Content "$pdir\settings.json" -Encoding UTF8 }
# Start Pulse the way a user does (the Start menu shortcut), or the exe with arguments and the test-mode variable
# when $testAlert is set (see --test-notify in TESTING.md).
function Start-Pulse([string]$testAlert = '', [switch]$NoWait) {
  if ($testAlert) {
    $env:PULSE_TEST_MODE = '1'
    Start-Process $appExe -ArgumentList '--test-notify', $testAlert
    Remove-Item Env:\PULSE_TEST_MODE
  } else {
    $lnk = Get-ChildItem "$env:APPDATA\Microsoft\Windows\Start Menu\Programs", "$env:USERPROFILE\Desktop", 'C:\Users\Public\Desktop' -Recurse -Filter *.lnk -ErrorAction SilentlyContinue |
      Where-Object { $_.Name -match 'Pulse|DigitalWellbeing' } | Select-Object -First 1
    if ($lnk) { Log "starting via shortcut $($lnk.FullName)"; Start-Process $lnk.FullName } else { Log 'no shortcut found, starting the exe'; Start-Process $appExe }
  }
  if ($NoWait) { return $null }
  return (Wait-Pulse-Window)
}
function Wait-Pulse-Window([int]$seconds = 60) {
  for ($i = 0; $i -lt $seconds; $i++) {
    $p = Get-Process DigitalWellbeing -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if ($p) { Start-Sleep 4; Log "Pulse window up, pid $($p.Id)"; return $p }
    Start-Sleep 1
  }
  Log 'Pulse window did not appear'; Shot 'no-window'; return $null
}
function Pulse-Log { Get-ChildItem $pdir -Recurse -Filter *.log -ErrorAction SilentlyContinue | Get-Content -ErrorAction SilentlyContinue }
function Run-Cli([string[]]$a) { $o = & $cliExe @a 2>&1 | ForEach-Object { "$_" }; return @{ Code = $LASTEXITCODE; Text = ($o -join "`n") } }

# ---------------------------------------------------------------- Pulse window via UI Automation
function Pulse-Hwnd {
  $p = Get-Process DigitalWellbeing -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $p) { return [IntPtr]::Zero }
  if ($p.MainWindowHandle -ne 0) { return $p.MainWindowHandle }
  return [W]::FindMain([uint32]$p.Id)
}
function Place-Pulse { $h = Pulse-Hwnd; [W]::ShowWindow($h, 9) | Out-Null; [W]::MoveWindow($h, 10, 10, 1500, 950, $true) | Out-Null; Start-Sleep 2 }
function Ui-Win { return [Windows.Automation.AutomationElement]::FromHandle((Pulse-Hwnd)) }
function Ui-Find([string]$id) {
  $c = New-Object Windows.Automation.PropertyCondition ([Windows.Automation.AutomationElement]::AutomationIdProperty), $id
  return (Ui-Win).FindFirst([Windows.Automation.TreeScope]::Descendants, $c)
}
function Ui-Click($el) { $r = $el.Current.BoundingRectangle; Click-At ([int]($r.X + $r.Width / 2)) ([int]($r.Y + $r.Height / 2)) }
function Exists-El([string]$id) { try { return ($null -ne (Ui-Find $id)) } catch { return $false } }
function Pulse-Title { try { return (Ui-Find 'PageTitle').Current.Name } catch { return '?' } }
function Pulse-Front { return ([W]::GetForegroundWindow() -eq (Pulse-Hwnd)) }
# Click a page in the rail and wait until the page title starts with $expect (any title when $expect is empty).
function Go-Page([string]$navId, [string]$expect = '') {
  $e = Ui-Find $navId; if (-not $e) { Log "$navId not found"; return $false }
  Ui-Click $e
  for ($i = 0; $i -lt 12; $i++) {
    Start-Sleep 1
    $t = Pulse-Title
    if ($t -and $t -ne '?' -and $t.StartsWith($expect)) { return $true }
  }
  return $false
}

# ---------------------------------------------------------------- standard step: logs
function Collect-Logs {
  Kill-Pulse
  Get-ChildItem $pdir -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -in '.log', '.json' } |
    ForEach-Object { Copy-Item $_.FullName (Join-Path $script:dir ("pulse_" + $_.Name)) -Force }
  $fb = @(Pulse-Log | Select-String 'Toast not available').Count
  Log "tray balloon fallback lines in the Pulse log: $fb"
  Get-WinEvent -LogName Application -MaxEvents 200 -ErrorAction SilentlyContinue |
    Where-Object { $_.ProviderName -match '\.NET Runtime|Application Error' -and $_.Message -match 'DigitalWellbeing|Pulse' } |
    ForEach-Object { Add-Content (Join-Path $script:dir 'crash-events.txt') ("{0} {1}" -f $_.TimeCreated, $_.Message.Substring(0, [Math]::Min(300, $_.Message.Length))) }
}
