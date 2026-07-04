# Changelog

## [v2.2.0] - 2026-07-05

Release-hardening: real data-consistency fixes, a Microsoft Store channel, an open-source relicense, and a repeatable screenshot pipeline.

### Data accuracy
- **Categories now count.** Focus-Mode app categories are attributed correctly on the Dashboard "Categories" tile and the Weekly Report "Focus vs Leisure" — previously everything showed as Uncategorized because lookups keyed on a process name but were queried by full path.
- **Consistent "today" everywhere.** The Dashboard, App Usage page, and reports now read today's totals through one shared source (persisted rows + the live session), so App Time and Screen Time agree across the app instead of drifting by up to the 5-minute save interval.
- **Corrupt rows can't poison totals.** Session writes are validated (no reversed intervals, no >24h durations) and reads filter bad rows; a clock/timezone change mid-session flushes the trackers so a day's usage can't split across buckets.
- **Truthful exports & recovery.** Sound CSV exports now include actual listening/harmful seconds (not just wall-clock span); a crashed focus session recovers to its last heartbeat instead of its full planned length.
- **Housekeeping.** Configurable retention (default 12 months) with a daily purge + VACUUM, and a Settings option to delete a specific date range.

### Microsoft Store
- Official **Microsoft Store** channel (MSIX) alongside the existing GitHub/Velopack build, from one codebase. Runtime channel detection disables self-update and switches launch-at-startup to the OS StartupTask API when installed from the Store.

### Open source & privacy
- Relicensed to **GPL-3.0** with third-party notices; added `PRIVACY.md`, `SECURITY.md`, `CONTRIBUTING.md`, a Code of Conduct, and issue/PR templates.

### Tooling
- Repeatable, DPI-correct **screenshot pipeline** (FlaUI + fixture seeder) — README/site screenshots are generated full and uncropped, in Light and Dark.
- CI: test-gated release pipeline (Velopack + MSIX + SHA256SUMS), PR build/test + site build, winget auto-publish, and single-sourced versioning from the git tag.

---

## [v2.1.0] - 2026-05-31

### Pulse Redesign

A complete visual redesign to the **Pulse** design language — a calm graphite base, soft solid surfaces with a 1px border and gentle shadow, generous 24px radii, and a per-section signature accent — recreated from the Pulse design reference.

### New Look
- **Rebranded to Pulse** - the app is now **Pulse - DigitalWellbeingPC** ("Pulse" in the UI, full name in About/installer). Existing data, settings, and logs **migrate automatically** on first launch, so updates and reinstalls keep all your history.
- **New shell** - a slim icon navigation rail and a top bar with a time-of-day greeting ("Good morning/afternoon/evening"), a live "Tracking" indicator, and a one-click theme toggle.
- **Every view rebuilt** to the Pulse layout: Today (Dashboard), Screen Time, App Usage, Hearing (Sound), Focus, Insights (Weekly Report), Settings, and Help.
  - Dashboard now leads with a goal ring, a "split across your top apps" bar, a most-used sparkline, and a this-week mini chart.
  - Two-column layouts with content centered to a comfortable max width.
- **Per-section signature accents** - each section carries its own calm hue (Dashboard indigo, Screen Time cyan, App Usage orchid, Hearing teal, Focus amber, Insights rose, Settings steel, Help slate), applied live as you navigate.
- **Refined light & dark themes** - a graphite/indigo dark palette and a soft-neutral light palette; per-section accents deepen slightly in light mode for legibility on white.
- **Rounded-rectangle controls** - buttons, duration chips, segmented tabs, and progress bars use clean rounded rectangles; small status badges stay pill-shaped.

### Improvements
- Fixed a screen-time calculation mismatch and tightened UI consistency.

### Technical
- New token-driven theming foundation (Pulse design tokens, palettes, and a per-section accent service).
- `ThemeService` now retints the live accent per section and is theme-aware (deeper hues in light mode).
- Added FlaUI-based UI smoke tests for the shell.
- Reconciled version metadata: `csproj`, `AssemblyInfo`, and the in-app version displays now all report **2.1.0**.

---

## [v2.0.0] - 2026-01-31

### Production Hardening and Feature Update

Major release with production-grade reliability improvements, new features, and UI refinements.

### New Features
- **Welcome Screen** - First-run onboarding experience
  - Introduces key features to new users
  - Only shown once (persisted via settings)

- **Help Section** - Built-in FAQ and troubleshooting
  - Common questions about screen time, app usage, and sound tracking
  - Accessible from the navigation sidebar

- **Data Export** - Export all tracking data to CSV
  - Screen time, app usage, and sound exposure
  - Date range selection
  - Opens file picker for save location

- **Week Navigation** - Browse past weeks in Screen Time view
  - Chevron buttons to go forward/back between weeks
  - Week label shows ISO week number and date range (e.g. "W5 . Jan 27-Feb 2")
  - Forward button disabled when on the current week

### Improvements
- **Dashboard auto-refresh** - Values now update immediately when switching back to Dashboard tab
  - Added IsVisibleChanged handler as backup for WPF Loaded/Unloaded events
  - Start/stop refresh methods are now idempotent (safe to call multiple times)

- **Proportional weekly bars** - Weekly usage bars now fill the container proportionally
  - Longest day fills the full width, other days scale relative to it
  - Replaced hardcoded 300px max width with dynamic percentage-based sizing

- **Consistent badge styling** - Neutral category badge in Focus view now uses the PillBadge style system
  - Matches corner radius and padding of Work and Entertainment badges

- **Version display** - Settings page now shows correct version from assembly info

### Technical
- Added `Properties/AssemblyInfo.cs` with proper version attributes
- Added `LogService` for structured application logging
- Added `app.manifest` with DPI awareness (Per-Monitor V2) and execution level config
- Extended test suite with `ScreenTimeTrackerExtendedTests` and `SettingsServiceTests`
- Cleaned up `.gitignore` (added `.claude/`, confirmed `docs/` exclusion)
- Removed stray test artifacts

---

## [v1.7.0] - 2025-06-20

### Design Revamp - Samsung Digital Wellbeing Style

Major visual overhaul matching Samsung Digital Wellbeing aesthetic.

### New Features
- **App Name Service** - Intelligent app name display
  - 80+ known apps mapped to friendly names (Chrome → Google Chrome, explorer → File Explorer)
  - Falls back to executable metadata for unknown apps
  - Prettifies raw process names with proper capitalization

- **View Toggle** - Today/Weekly view switching
  - Added functional toggle to Screen Time view
  - Visual state changes on selection
  - Framework for other views

- **Set Goal Button** - Dashboard quick action now works
  - Clicking "Set goal" navigates to Settings

### Visual Changes
- **Removed oval/pill shapes** - All progress bars now use flat rectangles
  - Stacked bars: 4px corner radius (was fully rounded)
  - Status badges: 8px corner radius (was pill-shaped)
  - Week selectors: 8px corner radius (was pill-shaped)
  - Comparison bars: 4px corner radius (was fully rounded)

- **Improved app list display**
  - App icons shown in colored containers
  - Proper display names instead of raw process names
  - Duration formatting maintained

- **Simplified charts**
  - Replaced LiveCharts horizontal bar chart with list-based progress bars
  - Replaced pie/donut chart with horizontal stacked bar
  - Added `PercentOfMax` for relative progress visualization

### New Components
- `Services/AppNameService.cs` - App name prettification
- `Converters/PercentToGridLengthConverter` - For stacked bar widths

### Bug Fixes
- Fixed "Most used apps" chart showing "0h" on all axes
- Fixed Focus vs Leisure chart showing raw decimal numbers
- Fixed dashboard app list missing icons and showing raw names

---

## [v1.5.1] - 2025-12-21

### Bug Fixes
- **Wind Down notification spam** - Fixed notification showing repeatedly on settings change
  - Notification now only shows once per Wind Down session (with 60-min throttle safety)
  - Settings changes no longer reset notification state
- **Break Reminder state reset** - Fixed timer resetting when changing settings
  - Timer state preserved when updating Break Reminder settings
- **System sleep/resume handling** - Added proper pause/resume for all services
  - Wind Down and Break Reminder now pause on screen lock and system sleep
  - Services resume correctly without notification spam on wake
- **Duplicate resize grips** - Fixed two resize grips appearing at window corner
  - Custom styled resize grip now inside the window border
- **Timer interval** - Fixed Break Reminder polling from 2s to 30s (reduced CPU usage)

### Improvements
- Custom resize grip styled to match app theme
- Better state management for all timer-based services

---

## [v1.5.0] - 2025-12-20

### Features
- **Wind Down Mode** - Subtle end-of-day awareness (non-blocking)
  - Scheduled quiet hours with configurable start/end time
  - Gentle notification when Wind Down starts ("Time to Wind Down")
  - Subtle visual border glow around window edges during quiet hours
  - Three visual styles: Amber (warm), Purple (calm), Dim (subtle)
  - All settings configurable in Settings view

### Wind Down Behavior
- **Non-blocking**: Wind Down only provides awareness, never blocks anything
- **Gradual**: Visual cue is gentle and non-distracting (30% opacity by default)
- **Overnight support**: Schedules like 9 PM to 7 AM work correctly
- Notification only shown once per Wind Down session
- Visual cue automatically appears/disappears based on schedule

### Settings
- Enable/disable Wind Down mode
- Set start and end times (30-minute increments)
- Toggle notification when Wind Down starts
- Toggle visual border effect
- Choose visual style (Amber/Purple/Dim)

---

## [v1.4.0] - 2025-12-20

### Features
- **Focus Sessions** - Windows-native Focus Mode with user-controlled enforcement
  - Start/stop focus sessions with customizable duration (15/25/45/60/90 min or custom)
  - Two enforcement levels: Warn (balloon notification), Block (auto-minimize distracting apps)
  - App categorization: Work, Entertainment, Neutral
  - Per-app category assignment from last 7 days of usage (sorted by usage time)
  - Real-time focus timer with progress bar
  - Distraction tracking with warnings and override counts
  - Focus session history with daily stats and completion status
  - Custom "End Session" confirmation dialog (not Windows MessageBox)
  - System tray balloon notifications for distraction warnings

### Enforcement Behavior
- **Warn Mode**: Shows tray balloon notification every 30 seconds if user stays on entertainment app
- **Block Mode**: Auto-minimizes entertainment apps every 5 seconds if user keeps restoring them
- Apps that resist minimize (admin/fullscreen) automatically fallback to Warn mode
- Clicking "Allow This App" permits that app for the rest of the session
- Clicking "Back to Work" allows re-warning if user returns to same app

### Edge Cases Handled
- System apps excluded from blocking (explorer, Task Manager, Start Menu, etc.)
- Cooldown-based warning system prevents spam while allowing repeated reminders
- Per-app tracking (switching apps doesn't reset warnings for other apps)
- Long app names show with ellipsis and tooltip

### Theme Improvements
- Duration buttons update both background AND foreground when selected
- Enforcement options (Warn/Block) have proper selected state colors
- Custom dialogs match app's dark/light theme

### Technical
- Consolidated NativeMethods into Platform/Windows folder
- Added SW_FORCEMINIMIZE for aggressive window minimize
- Debug logging for focus check behavior

---

## [v1.3.0] - 2025-12-19

### Features
- **Break Reminders** - Periodic reminders following the 20-20-20 rule
  - Centered overlay dialog when app is visible
  - System tray balloon notification when minimized
  - Snooze (5 min, max 3 times) and Dismiss controls
  - "Turn off reminders" quick action in overlay
  - User idle detection - idle time counts as break
  - Configurable intervals in Settings

### Improvements
- Proper notification state management (no duplicate alerts)
- Overlay auto-shows when app restored with pending break
- Theme-aware overlay design (Dark/Light)

---

## [v1.2.0] - 2025-12-19

### Features

- Complete modern UI redesign with Material Design
- Dark/Light/Auto theme switching with system integration
- Daily screen time goals with progress tracking
- Screen time timeline visualization
- App usage real-time refresh and focus metrics
- Sound exposure monitoring with SPL calculation
- System tray support with minimize to tray
- Single instance enforcement
- Session tracking with idle detection
- Auto-updates via Velopack

### Technical

- MVVM architecture with INotifyPropertyChanged
- SQLite for local data storage
- Win32 API integration for focus tracking
- NAudio for sound monitoring
- DispatcherTimer for UI refresh
- Event-driven app switching

---

## [v1.1.0] - 2025-12-11

- New modern dashboard with overview cards
- App usage tracking with focus metrics (switches, avg focus, longest session)
- Screen time daily timeline and weekly breakdown
- Sound exposure monitoring with harmful exposure alerts
- Daily screen time goals
- Light, Dark, and Auto theme support
- Auto-updates via Velopack
- Launch on Windows startup option
- Improved UI with Material Design

## [v0.0.1] - 2024-04-03

- Added screen time live graph
- Refactored to MVVM
- SQLite session tracking
