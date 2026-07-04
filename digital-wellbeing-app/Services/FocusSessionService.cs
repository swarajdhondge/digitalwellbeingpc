using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Platform.Windows;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Service for managing Focus Sessions with user-controlled enforcement
    /// Modes: Warn (popup), Block (prevent), Hide (remove from taskbar - future)
    /// </summary>
    public class FocusSessionService : IDisposable
    {
        private readonly System.Timers.Timer _sessionTimer;
        private readonly System.Timers.Timer _focusCheckTimer;
        private FocusSession? _currentSession;
        private bool _isDisposed = false;
        private readonly Dictionary<string, AppCategoryType> _appCategories;
        private readonly HashSet<string> _appsFailedToBlock = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _sessionOverrideApps = new(StringComparer.OrdinalIgnoreCase);
        
        // Track when each app was last warned/blocked (to allow re-warning after timeout)
        private readonly Dictionary<string, DateTime> _appLastActionTime = new(StringComparer.OrdinalIgnoreCase);
        
        // How long to wait before re-warning about the same app (if user ignores)
        private static readonly TimeSpan WarnCooldown = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan BlockCooldown = TimeSpan.FromSeconds(5); // Re-minimize faster

        // Persist a heartbeat this often so crash recovery ends orphaned sessions near their real
        // end instead of the fabricated planned duration.
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60);
        private DateTime _lastHeartbeatUtc = DateTime.MinValue;

        /// <summary>
        /// System apps that should never be blocked (Windows shell, our app, etc.)
        /// </summary>
        private static readonly HashSet<string> SystemExclusionList = new(StringComparer.OrdinalIgnoreCase)
        {
            "DigitalWellbeing",       // Our app
            "digital-wellbeing-app",  // Our app (alternate name)
            "explorer",               // Windows Shell
            "ShellExperienceHost",    // Windows Taskbar
            "StartMenuExperienceHost",// Windows Start Menu
            "SearchHost",             // Windows Search
            "SearchUI",               // Windows Search (older)
            "LockApp",                // Windows Lock Screen
            "SystemSettings",         // Windows Settings
            "ApplicationFrameHost",   // UWP host
            "TextInputHost",          // Windows keyboard
            "RuntimeBroker",          // Windows runtime
            "dwm",                    // Desktop Window Manager
            "csrss",                  // Client/Server Runtime
            "svchost",                // Service Host
            "taskmgr",                // Task Manager (always allow!)
            "Taskmgr",                // Task Manager (case variant)
        };

        /// <summary>Fired when a focus session starts</summary>
        public event Action? SessionStarted;

        /// <summary>Fired when a focus session ends</summary>
        public event Action<bool>? SessionEnded; // bool = completed normally

        /// <summary>Fired every second during active session (for timer updates)</summary>
        public event Action? SessionTick;

        /// <summary>Fired when a distracting app is detected</summary>
        public event Action<string, string>? DistractingAppDetected; // appName, executablePath

        /// <summary>Current focus session (null if not in focus mode)</summary>
        public FocusSession? CurrentSession => _currentSession;

        /// <summary>Whether a focus session is currently active</summary>
        public bool IsInFocusMode => _currentSession != null;

        /// <summary>Time remaining in current session</summary>
        public TimeSpan TimeRemaining
        {
            get
            {
                if (_currentSession == null) return TimeSpan.Zero;
                var elapsed = DateTime.Now - _currentSession.StartTime;
                var planned = TimeSpan.FromMinutes(_currentSession.PlannedDurationMinutes);
                var remaining = planned - elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        /// <summary>Time elapsed in current session</summary>
        public TimeSpan TimeElapsed
        {
            get
            {
                if (_currentSession == null) return TimeSpan.Zero;
                return DateTime.Now - _currentSession.StartTime;
            }
        }

        /// <summary>Progress percentage (0-100)</summary>
        public double Progress
        {
            get
            {
                if (_currentSession == null) return 0;
                var elapsed = TimeElapsed.TotalMinutes;
                var planned = _currentSession.PlannedDurationMinutes;
                return Math.Min(100, (elapsed / planned) * 100);
            }
        }

        // Settings
        public FocusEnforcementLevel EnforcementLevel { get; set; } = FocusEnforcementLevel.Warn;
        public int DefaultDurationMinutes { get; set; } = 25;
        public bool BlockEntertainment { get; set; } = true;
        public bool AllowWorkApps { get; set; } = true;
        public bool SoundOnComplete { get; set; } = true;

        public FocusSessionService()
        {
            _appCategories = new Dictionary<string, AppCategoryType>(StringComparer.OrdinalIgnoreCase);

            // Timer for session countdown (fires every second)
            _sessionTimer = new System.Timers.Timer(1000)
            {
                AutoReset = true
            };
            _sessionTimer.Elapsed += OnSessionTimerTick;

            // Timer for checking foreground app (fires every 2 seconds)
            _focusCheckTimer = new System.Timers.Timer(2000)
            {
                AutoReset = true
            };
            _focusCheckTimer.Elapsed += OnFocusCheck;

            LoadSettings();
            LoadAppCategories();
            RecoverOrphanedSessions();
        }

        /// <summary>
        /// Check for focus sessions that were started but never ended (crash recovery).
        /// Marks them as incomplete so they show in history.
        /// </summary>
        private void RecoverOrphanedSessions()
        {
            try
            {
                var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
                var todaySessions = DatabaseService.GetFocusSessionsForDate(DateTime.Today);

                foreach (var session in todaySessions)
                {
                    // Orphaned = still active (no real end time) and never completed. Handle both
                    // null and MinValue, since older rows may have persisted either.
                    var hasEnd = session.EndTime.HasValue && session.EndTime.Value > DateTime.MinValue;
                    if (!hasEnd && !session.Completed)
                    {
                        // Recover to the last heartbeat (when the app was last alive) rather than
                        // fabricating the full planned duration. Fall back to planned end for legacy
                        // rows that predate the heartbeat, and never end before the session started.
                        var recoveredEnd = session.LastSeenUtc > DateTime.MinValue
                            ? session.LastSeenUtc.ToLocalTime()
                            : session.StartTime.AddMinutes(session.PlannedDurationMinutes);
                        if (recoveredEnd < session.StartTime)
                            recoveredEnd = session.StartTime;

                        session.EndTime = recoveredEnd;
                        session.Completed = false;
                        DatabaseService.SaveFocusSession(session);
                        System.Diagnostics.Debug.WriteLine($"[Focus] Recovered orphaned session from {session.StartTime} to {recoveredEnd}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Focus] Orphan recovery error: {ex.Message}");
            }
        }

        /// <summary>
        /// Start a new focus session
        /// </summary>
        /// <param name="durationMinutes">Duration in minutes</param>
        public void StartSession(int durationMinutes)
        {
            if (_currentSession != null)
            {
                // End existing session first
                EndSession(false);
            }

            _currentSession = new FocusSession
            {
                StartTime = DateTime.Now,
                PlannedDurationMinutes = durationMinutes,
                EnforcementLevel = EnforcementLevel,
                SessionDate = DateTime.Now.ToString("yyyy-MM-dd"),
                Completed = false,
                LastSeenUtc = DateTime.UtcNow
            };
            _lastHeartbeatUtc = DateTime.UtcNow;

            // Persist immediately so crash recovery can find it
            DatabaseService.SaveFocusSession(_currentSession);

            // Clear session-specific data from previous sessions
            _sessionOverrideApps.Clear();
            _appsFailedToBlock.Clear();
            _appLastActionTime.Clear();

            _sessionTimer.Start();
            _focusCheckTimer.Start();

            SessionStarted?.Invoke();
        }

        /// <summary>
        /// End the current focus session
        /// </summary>
        /// <param name="completed">Whether the session completed normally</param>
        public void EndSession(bool completed)
        {
            if (_currentSession == null) return;

            _sessionTimer.Stop();
            _focusCheckTimer.Stop();

            _currentSession.EndTime = DateTime.Now;
            _currentSession.Completed = completed;

            // Save to database
            DatabaseService.SaveFocusSession(_currentSession);

            var session = _currentSession;
            _currentSession = null;

            if (completed && SoundOnComplete)
            {
                System.Media.SystemSounds.Asterisk.Play();
            }

            SessionEnded?.Invoke(completed);
        }

        /// <summary>
        /// Check if an app is considered distracting based on its category
        /// </summary>
        public bool IsDistractingApp(string appIdentifier)
        {
            if (string.IsNullOrEmpty(appIdentifier)) return false;

            // Collapse process name / path variants to one canonical key.
            appIdentifier = AppIdentity.NormalizeKey(appIdentifier);
            if (appIdentifier.Length == 0) return false;

            // Never block system apps (exclusion entries are already extension-less process names)
            if (SystemExclusionList.Contains(appIdentifier))
                return false;

            // Check if user has overridden this app for the current session
            if (_sessionOverrideApps.Contains(appIdentifier))
                return false;

            // Get category for this app
            if (_appCategories.TryGetValue(appIdentifier, out var category))
            {
                // Work apps are always allowed if setting is enabled
                if (AllowWorkApps && category == AppCategoryType.Work)
                    return false;

                // Entertainment apps are distracting
                if (BlockEntertainment && category == AppCategoryType.Entertainment)
                    return true;
            }

            // Uncategorized/Neutral apps are not blocked by default
            return false;
        }

        /// <summary>
        /// Set the category for an app
        /// </summary>
        public void SetAppCategory(string appIdentifier, string appName, string executablePath, AppCategoryType category)
        {
            // Store under the canonical key so runtime enforcement (keyed by process name) and
            // the reports (keyed by normalized executable path) both resolve to the same row.
            // Honor the caller's explicit identifier first, falling back to the path/name.
            var key = AppIdentity.NormalizeKey(appIdentifier);
            if (key.Length == 0) key = AppIdentity.NormalizeKey(executablePath, appName);
            if (key.Length == 0) return;

            _appCategories[key] = category;

            // Save to database
            DatabaseService.SaveAppCategory(new AppCategory
            {
                AppIdentifier = key,
                AppName = appName,
                ExecutablePath = executablePath,
                Category = category,
                LastUpdated = DateTime.Now
            });
        }

        /// <summary>
        /// Get category for an app
        /// </summary>
        public AppCategoryType GetAppCategory(string appIdentifier)
        {
            var key = AppIdentity.NormalizeKey(appIdentifier);
            if (_appCategories.TryGetValue(key, out var category))
                return category;
            return AppCategoryType.Uncategorized;
        }

        /// <summary>
        /// Get all categorized apps
        /// </summary>
        public Dictionary<string, AppCategoryType> GetAllAppCategories()
        {
            return new Dictionary<string, AppCategoryType>(_appCategories);
        }

        /// <summary>
        /// Record that a distraction warning was shown
        /// </summary>
        public void RecordDistractionWarning()
        {
            if (_currentSession != null)
            {
                _currentSession.DistractionWarnings++;
            }
        }

        /// <summary>
        /// Record that user overrode a warning to continue using a distracting app
        /// </summary>
        public void RecordDistractionOverride()
        {
            if (_currentSession != null)
            {
                _currentSession.DistractionOverrides++;
            }
        }

        /// <summary>
        /// Allow a specific app for the remainder of this focus session.
        /// Called when user clicks "Continue Anyway" on the warning.
        /// </summary>
        public void AllowAppForSession(string appIdentifier)
        {
            var key = AppIdentity.NormalizeKey(appIdentifier);
            if (key.Length > 0)
            {
                _sessionOverrideApps.Add(key);
                // Remove from action tracking since it's now allowed
                _appLastActionTime.Remove(key);
                System.Diagnostics.Debug.WriteLine($"[Focus] App allowed for session: {key}");
            }
        }

        /// <summary>
        /// Called when user dismisses warning (goes back to work).
        /// Resets cooldown so if they return to the same app, they get warned again.
        /// </summary>
        public void DismissWarning(string appIdentifier)
        {
            // Remove the cooldown entry so next time they go to this app, they get warned immediately
            var key = AppIdentity.NormalizeKey(appIdentifier);
            if (key.Length > 0)
            {
                _appLastActionTime.Remove(key);
                System.Diagnostics.Debug.WriteLine($"[Focus] Warning dismissed for: {key}");
            }
        }

        /// <summary>
        /// Check if an app has been overridden (allowed) for this session
        /// </summary>
        public bool IsAppOverriddenForSession(string appIdentifier)
        {
            return _sessionOverrideApps.Contains(AppIdentity.NormalizeKey(appIdentifier));
        }

        /// <summary>
        /// Get focus session history
        /// </summary>
        public List<FocusSession> GetSessionHistory(DateTime date)
        {
            return DatabaseService.GetFocusSessionsForDate(date);
        }

        /// <summary>
        /// Get total focus time for a date
        /// </summary>
        public TimeSpan GetTotalFocusTime(DateTime date)
        {
            var sessions = GetSessionHistory(date);
            return TimeSpan.FromMinutes(sessions.Sum(s => s.Duration.TotalMinutes));
        }

        public void LoadSettings()
        {
            var settingsService = new SettingsService();

            EnforcementLevel = settingsService.LoadFocusEnforcementLevel();
            DefaultDurationMinutes = settingsService.LoadFocusDefaultDuration();
            BlockEntertainment = settingsService.LoadFocusBlockEntertainment();
            AllowWorkApps = settingsService.LoadFocusAllowWorkApps();
            SoundOnComplete = settingsService.LoadFocusSoundOnComplete();
        }

        public void SaveSettings()
        {
            var settingsService = new SettingsService();

            settingsService.SaveFocusEnforcementLevel(EnforcementLevel);
            settingsService.SaveFocusDefaultDuration(DefaultDurationMinutes);
            settingsService.SaveFocusBlockEntertainment(BlockEntertainment);
            settingsService.SaveFocusAllowWorkApps(AllowWorkApps);
            settingsService.SaveFocusSoundOnComplete(SoundOnComplete);
        }

        private void LoadAppCategories()
        {
            try
            {
                var categories = DatabaseService.GetAllAppCategories();
                foreach (var cat in categories)
                {
                    // Normalize on load so legacy rows (path-keyed) map to the canonical key.
                    var key = AppIdentity.NormalizeKey(cat.AppIdentifier);
                    if (key.Length > 0)
                        _appCategories[key] = cat.Category;
                }
            }
            catch
            {
                // Ignore errors during initial load
            }
        }

        private void OnSessionTimerTick(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_currentSession == null) return;

            // Check if session time is complete
            if (TimeRemaining <= TimeSpan.Zero)
            {
                EndSession(true);
                return;
            }

            // Persist a lightweight heartbeat so a crash mid-session recovers to (roughly) now.
            var utcNow = DateTime.UtcNow;
            if (utcNow - _lastHeartbeatUtc >= HeartbeatInterval)
            {
                _lastHeartbeatUtc = utcNow;
                _currentSession.LastSeenUtc = utcNow;
                try { DatabaseService.SaveFocusSession(_currentSession); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Focus] Heartbeat save failed: {ex.Message}"); }
            }

            SessionTick?.Invoke();
        }

        private void OnFocusCheck(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_currentSession == null) return;

            try
            {
                // Get current foreground process
                var foregroundHandle = NativeMethods.GetForegroundWindow();
                if (foregroundHandle == IntPtr.Zero) return;

                NativeMethods.GetWindowThreadProcessId(foregroundHandle, out uint processId);
                if (processId == 0) return;

                using var process = Process.GetProcessById((int)processId);
                if (process == null) return;

                // displayName is shown in the warning UI; appIdentifier is the canonical
                // internal key used for category lookup, cooldowns and session overrides.
                var displayName = process.ProcessName;
                var appIdentifier = AppIdentity.NormalizeKey(displayName);

                // Check if this is a distracting app
                if (!IsDistractingApp(appIdentifier))
                {
                    return; // Not distracting, nothing to do
                }

                // Check cooldown - have we recently warned/blocked this app?
                var cooldown = EnforcementLevel == FocusEnforcementLevel.Warn ? WarnCooldown : BlockCooldown;

                if (_appLastActionTime.TryGetValue(appIdentifier, out var lastAction))
                {
                    var elapsed = DateTime.Now - lastAction;
                    if (elapsed < cooldown)
                    {
                        // Still in cooldown, skip
                        return;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[Focus] DISTRACTING APP DETECTED: {appIdentifier} (Enforcement: {EnforcementLevel})");

                string execPath = string.Empty;
                try { execPath = process.MainModule?.FileName ?? string.Empty; }
                catch { }

                // Update last action time for this app
                _appLastActionTime[appIdentifier] = DateTime.Now;

                // Handle based on enforcement level
                switch (EnforcementLevel)
                {
                    case FocusEnforcementLevel.Warn:
                        System.Diagnostics.Debug.WriteLine($"[Focus] Warn mode - showing notification");
                        RecordDistractionWarning();
                        DistractingAppDetected?.Invoke(displayName, execPath);
                        break;

                    case FocusEnforcementLevel.Block:
                    case FocusEnforcementLevel.Hide:
                        System.Diagnostics.Debug.WriteLine($"[Focus] Block mode - attempting to minimize");
                        // Try to minimize the distracting app
                        bool minimizeSucceeded = TryMinimizeWindow(foregroundHandle, appIdentifier);
                        System.Diagnostics.Debug.WriteLine($"[Focus] Minimize result: {minimizeSucceeded}");
                        
                        // Show notification (only if minimize failed OR first time)
                        RecordDistractionWarning();
                        DistractingAppDetected?.Invoke(displayName, execPath);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Focus] Error in focus check: {ex.Message}");
            }
        }

        /// <summary>
        /// Try to minimize a window. Returns false if the app resisted (admin, full-screen, UWP, etc.)
        /// </summary>
        private bool TryMinimizeWindow(IntPtr windowHandle, string appIdentifier)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[Focus] Attempting to minimize: {appIdentifier}");

                // Check if we already know this app can't be blocked
                if (_appsFailedToBlock.Contains(appIdentifier))
                {
                    System.Diagnostics.Debug.WriteLine($"[Focus] App already in failed list: {appIdentifier}");
                    return false;
                }

                // Try regular minimize first
                NativeMethods.ShowWindow(windowHandle, NativeMethods.SW_MINIMIZE);
                
                // Brief delay to let Windows process the minimize command
                System.Threading.Thread.Sleep(200);
                
                // Verify it actually minimized by checking if it's still foreground
                var currentForeground = NativeMethods.GetForegroundWindow();
                if (currentForeground == windowHandle)
                {
                    // Try force minimize
                    System.Diagnostics.Debug.WriteLine($"[Focus] Regular minimize failed, trying force minimize: {appIdentifier}");
                    NativeMethods.ShowWindow(windowHandle, NativeMethods.SW_FORCEMINIMIZE);
                    System.Threading.Thread.Sleep(200);
                    
                    currentForeground = NativeMethods.GetForegroundWindow();
                    if (currentForeground == windowHandle)
                    {
                        // Still in foreground - minimize didn't work (admin app, full-screen game, etc.)
                        System.Diagnostics.Debug.WriteLine($"[Focus] Force minimize also failed: {appIdentifier}");
                        _appsFailedToBlock.Add(appIdentifier);
                        return false;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[Focus] Successfully minimized: {appIdentifier}");
                return true;
            }
            catch (Exception ex)
            {
                // Any error means we couldn't minimize
                System.Diagnostics.Debug.WriteLine($"[Focus] Minimize error for {appIdentifier}: {ex.Message}");
                _appsFailedToBlock.Add(appIdentifier);
                return false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _sessionTimer?.Stop();
            _sessionTimer?.Dispose();
            _focusCheckTimer?.Stop();
            _focusCheckTimer?.Dispose();

            // End any active session
            if (_currentSession != null)
            {
                EndSession(false);
            }

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }

}

