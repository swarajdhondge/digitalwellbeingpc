# Changelog

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
