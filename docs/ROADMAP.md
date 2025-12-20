# Roadmap - Digital Wellbeing PC

## Philosophy
- User control over enforcement (options, not mandates)
- Subtle awareness (inform, don't block work)
- Reports before raw export (insights first)
- Windows-native experience (not Android clone)

---

## Current State (v1.2)

- [x] Core tracking (Screen Time, Sound, App Usage)
- [x] Modern UI redesign (Material Design)
- [x] Dark/Light/Auto theme switching
- [x] Single instance lock
- [x] Daily screen time goals
- [x] Screen time timeline rendering
- [x] App usage real-time refresh
- [x] System tray with minimize

---

## v1.3 - Break Reminders
**Priority: HIGH | Status: COMPLETED**

- [x] Break reminder service (20-20-20 rule)
- [x] Centered overlay dialog when app visible
- [x] System tray balloon notification when minimized
- [x] Configurable intervals (15/20/30/45/60 min)
- [x] Sound + visual notification options
- [x] Snooze/dismiss controls with snooze limit (3 max)
- [x] "Turn off reminders" quick action in overlay
- [x] User idle detection (idle time = natural break)
- [x] Proper state management (no duplicate notifications)
- [x] Auto-show overlay when app restored with pending break

**Components:**
- `Services/BreakReminderService.cs` - Timer, idle detection, snooze limits
- `Models/BreakReminderSettings.cs`
- Break overlay in MainWindow.xaml (theme-aware)
- Settings UI section

---

## v1.4 - Focus Sessions
**Priority: HIGH | Status: Next**

Windows-native Focus Mode with user-controlled enforcement:

| Mode | Behavior |
|------|----------|
| Warn | Popup when opening distracting app (allow override) |
| Block | Prevent launch during focus time |
| Hide | Remove from taskbar/Start (softest) |

- [ ] Focus Session service with timer
- [ ] App categorization (Work/Entertainment/Uncategorized)
- [ ] Start/stop UI in sidebar or Dashboard
- [ ] Per-app category assignment
- [ ] Focus history tracking

**Components:**
- `Services/FocusSessionService.cs`
- `Models/AppCategory.cs`
- `Models/FocusSession.cs`
- `Views/Focus/FocusView.xaml`
- Settings: enforcement level, app selection

---

## v1.5 - Wind Down Mode
**Priority: MEDIUM**

Subtle end-of-day awareness (non-blocking):

- [ ] Scheduled quiet hours (configurable time)
- [ ] Subtle visual cue (colored border or dim overlay)
- [ ] Reminder notification ("Time to wind down")
- [ ] Optional: mute non-essential notifications

**Components:**
- `Services/WindDownService.cs`
- `Models/WindDownSettings.cs`
- Overlay window (low opacity border)
- Settings: schedule, visual style, toggle

---

## v1.6 - Weekly Reports
**Priority: MEDIUM**

Visual summaries (not raw export):

- [ ] Weekly summary card on Dashboard
- [ ] Daily screen time trend chart
- [ ] Top apps breakdown
- [ ] Focus time vs leisure comparison
- [ ] Week-over-week comparison

**Components:**
- `Views/Reports/WeeklyReportView.xaml`
- `ViewModels/WeeklyReportViewModel.cs`
- `Services/ReportService.cs`
- Chart controls (LiveCharts2 or custom)

---

## v1.7 - App Limits
**Priority: MEDIUM**

Per-app daily limits with user-controlled enforcement:

| Mode | Behavior |
|------|----------|
| Notify | Just tell me when limit reached |
| Warn | Show warning popup, allow override |
| Block | Hard block after limit |

- [ ] Per-app limit configuration
- [ ] Limit tracking in AppUsageTracker
- [ ] Warning/block popup
- [ ] Limit progress in App Usage view

**Components:**
- `Services/AppLimitService.cs`
- `Models/AppLimit.cs`
- Extend `AppUsageTracker` with limit hooks
- Settings: per-app limit UI

---

## v2.0 - Polish & Advanced
**Priority: LOW**

- [ ] Data Export (CSV/JSON) as sub-feature of Reports
- [ ] App categories management UI
- [ ] Monthly trend charts
- [ ] Session count tracking (sit-down sessions)
- [ ] Productivity score (optional)

---

## v3.0 - Platform Expansion
**Priority: FUTURE**

- [ ] Windows 11 widget
- [ ] Browser extension (site-level tracking)
- [ ] Multi-monitor awareness
- [ ] Localization (i18n)

---

## Tech Debt (Ongoing)

- [x] Unit tests for core logic (3 tests)
- [ ] Add more test coverage
- [ ] Structured logging (Serilog)
- [ ] Refactor to dependency injection
- [ ] Code documentation

---

## Priority Order

1. v1.3 Break Reminders - Quick win, high impact
2. v1.4 Focus Sessions - Flagship feature
3. v1.5 Wind Down - Unique differentiator
4. v1.6 Weekly Reports - User-facing value
5. v1.7 App Limits - Core wellbeing feature

---

## Notes

- Test on both Dark and Light themes
- Maintain existing design system tokens
- NotificationArea already reserved in `MainWindow.xaml`
- All new features should follow MVVM pattern
- Use existing SettingsService for persistence
