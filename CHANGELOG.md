# Changelog

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
