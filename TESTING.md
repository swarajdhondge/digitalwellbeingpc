# Testing Pulse

Test each part at the cheapest level that proves it.

- Pure logic and mappings: unit tests, every case.
- Each real Windows path (install, toast, tray, Store package): one end-to-end check in Windows Sandbox.
- Setup.exe and the Store package are different paths. Check each one on its own.
- No test for coverage's sake, and no duplicates.

## The three layers

| Layer | What | When it runs |
|---|---|---|
| Unit | xUnit tests for the app and the `pulse` tool | Every PR (CI) and the release workflow |
| UI and soak | FlaUI tests that drive the real built app: `ShellTests` (pages, navigation) and `SoakTests` (kill and relaunch with a Focus session) | The release workflow only: on a `v*.*.*` tag, or by hand with the build-only trigger. A failure stops the release |
| Sandbox release checks | The installed app in a clean Windows Sandbox: install, alerts, upgrade, uninstall | By hand, on the release build, before the release is published |

## How to run each layer

Unit (needs the .NET 9 SDK and Windows):

```powershell
dotnet test digital-wellbeing-app/Tests/Tests.csproj
dotnet test pulse-cli/Tests/pulse-cli.Tests.csproj
```

UI and soak (needs an interactive desktop and a built app; one class at a time, because each kills every running Pulse):

```powershell
dotnet build DigitalWellbeing.sln -c Release
$env:DW_APP_EXE = "$PWD\digital-wellbeing-app\bin\Release\net9.0-windows10.0.26100.0\DigitalWellbeing.exe"
dotnet test UITests/UITests.csproj -c Release --no-build --filter "FullyQualifiedName~ShellTests"
dotnet test UITests/UITests.csproj -c Release --no-build --filter "FullyQualifiedName~SoakTests"
```

Sandbox release checks need a Windows 11 Pro machine with Windows Sandbox enabled. Start them from the
signed-in desktop session, because Windows Sandbox does not open anywhere else (see `scripts/sandbox/launch.cmd`).

```powershell
scripts\sandbox\run-release-check.ps1 -Setup <path to Setup.exe> -Previous <previous tag> -Checks smoke
scripts\sandbox\run-release-check.ps1 -Setup <path to Setup.exe> -Previous <previous tag> -Checks smoke,alerts,tracking,ui,upgrade
scripts\sandbox\run-release-check.ps1 -Setup <path to Setup.exe> -Msix <path to .msix> -Checks msix
```

The script writes `PASS` and `FAIL` lines to `RESULTS.txt` in its run folder, with logs and screenshots. Run one
sandbox at a time.

### Test-only alert switch

Waiting for a real alert takes minutes. A test run can show one at once:

```powershell
$env:PULSE_TEST_MODE = '1'
DigitalWellbeing.exe --test-notify goal     # goal, limit, break, winddown, focus, hearing or hint
```

The alert goes through the same code and carries the same launch argument as the real one, so a click on it
behaves the same. The switch does nothing unless `PULSE_TEST_MODE=1` is set. Only the test scripts set it. Pulse
and its installers never do, so a normal launch cannot use it.

## What each part needs

Level: U = unit test, UI = FlaUI test (release workflow), S = Windows Sandbox check.

| Component | What can break | Proof | Level |
|---|---|---|---|
| Screen time counting | Idle or media handling, double counting | `ScreenTimeTrackerTests`; `tracking` | U, S |
| App usage and focus tracking | Wrong app credited, shell windows | `AppUsageTrackerTests`, `FocusChangeListenerTests` | U |
| Day change and clock change | Lost or split time at midnight | `ScreenTimeTrackerTests`, `AppUsageTrackerTests`; `tracking` | U, S |
| Hearing exposure | Wrong duration or alert | `SoundExposureManagerTests`, `SoundMonitoringServiceTests` | U |
| Database, retention, validation | Corrupt or lost rows | `DatabaseServiceTests` | U |
| Settings, backup, export | Lost or unreadable data | `SettingsServiceTests`, `BackupServiceTests`, `DataExportServiceTests` | U |
| Reports and categories | Totals that disagree | `ReportServiceTests`, `CategoryRuleServiceTests`, `LiveUsageProviderTests` | U |
| Goals, limits, breaks, wind-down, focus rules | Alert at the wrong time | `GoalServiceTests`, `AppLimitServiceTests`, `BreakReminderServiceTests`, `WindDownServiceTests`, `FocusSessionServiceTests` | U |
| Alert click routing | A click opens the wrong place | `NotificationRoutingTests`; `alerts` | U, S |
| Toast or tray balloon shows | No alert reaches the user | `smoke`, `alerts` (Setup.exe); `msix` (Store package) | S |
| Test switch | Alert name or guard parsed wrongly | `TestNotifyRequestTests` | U |
| `pulse` command-line tool | Wrong output, changed data | `CliTests`; `smoke` runs the installed tool | U, S |
| PATH entry for `pulse` | Folder added twice or other entries changed | `CommandLinePathTests` | U |
| Translations | Missing key, broken placeholder | `LocTests` | U |
| Window, pages, navigation | A page is missing or empty | `ShellTests`; `smoke` | UI, S |
| Relaunch after a crash | Hang or crash with an orphaned session | `FocusSessionServiceTests`, `SoakTests` | U, UI |
| Setup.exe install and uninstall | Install fails, files left behind | `smoke` | S |
| Update over an older version | Data or settings lost | `upgrade` | S |
| Store package (MSIX) | Package does not install, alert or alias fails | `msix` | S |
| In-app update | Update not offered or not applied | Post-release check below | manual |
| Tray icon and menu, start with Windows | Not covered by a script | Look at it before a release that touches them | manual |
| Look of the pages (light and dark) | Layout or colour drift | Compare with the README screenshots (`scripts/capture-screenshots.ps1`) | manual |

## Release checklist

1. CI is green on the release branch. The release workflow runs unit, UI and soak tests, then builds Setup.exe.
2. Run the smoke set on the release build: `-Checks smoke`.
3. Add the checks for the areas the release diff touches:
   - tracking code (`CoreLogic/`, idle, clock): `tracking`
   - alerts, notifications, `MainWindow` routing: `alerts`
   - pages, navigation, controls: `ui`
   - installer, Velopack, startup, data format: `upgrade` (and check the old data still opens)
   - anything in `digital-wellbeing-app.Package/` or the manifest: `msix`
4. Fix every `FAIL`, or write down why it is not a bug. Do not publish on a `FAIL`.
5. Before the Store upload, build the package (`scripts/build-msix.ps1 -Version <x.y.z>`) and run `-Checks msix` on it.

## After the release

1. GitHub release: Setup.exe, the full and delta packages and `RELEASES` are attached.
2. The website download page points to the new Setup.exe and shows the new version.
3. In-app update: install the previous release, let it find the update, and confirm the new version runs and keeps its data.
4. Store listing: the new version is live after certification, and the listing text and screenshots are right.
