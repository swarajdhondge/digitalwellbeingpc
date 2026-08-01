using System;
using System.Diagnostics;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Platform.Windows;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.CoreLogic
{
    public class AppUsageTracker : IDisposable
    {
        private AppUsageSession? _currentSession;
        private readonly FocusChangeListener _focusListener;
        private readonly System.Timers.Timer _periodicSaveTimer;
        private readonly object _sessionLock = new();
        private DateTime _lastSaved = DateTime.Now;

        // Save interval matches ScreenTimeTracker (5 minutes)
        private const int SaveIntervalMinutes = 5;

        /// <summary>
        /// The currently active app session (null if no app is focused or user is idle)
        /// </summary>
        public AppUsageSession? CurrentSession => _currentSession;

        /// <summary>
        /// Fired when the focused app changes
        /// </summary>
        public event Action? OnAppSwitched;

        /// <summary>
        /// Source of "now" for every day-boundary/session decision after construction. Defaults
        /// to the real clock; overridable for long-running simulation tests. Mirrors
        /// ScreenTimeTracker.Clock: the _lastSaved field initializer above intentionally still
        /// uses the real DateTime.Now (it runs before a caller can override this property).
        /// </summary>
        public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

        public AppUsageTracker()
        {
            _focusListener = new FocusChangeListener(OnAppChanged);

            // Periodic save timer to prevent data loss on crash
            _periodicSaveTimer = new System.Timers.Timer(60_000) // Check every minute
            {
                AutoReset = true
            };
            _periodicSaveTimer.Elapsed += OnPeriodicSave;
        }

        public void Start()
        {
            _focusListener.Start();
            _periodicSaveTimer.Start();
            SynthesizeInitialFocus();
        }

        /// <summary>
        /// SetWinEventHook (behind _focusListener) only fires on *future* foreground changes - the
        /// app already in the foreground when Pulse launches would otherwise get zero
        /// AppUsageSession credit until the user switches away and back. Synthesizes one initial
        /// resolution using the same foreground-process lookup FocusSessionService.OnFocusCheck
        /// already performs. OnAppChanged takes ownership of disposing the Process, same as it
        /// does for every real hook callback.
        /// </summary>
        private void SynthesizeInitialFocus()
        {
            try
            {
                var foregroundHandle = NativeMethods.GetForegroundWindow();
                if (foregroundHandle == IntPtr.Zero) return;

                NativeMethods.GetWindowThreadProcessId(foregroundHandle, out uint processId);
                if (processId == 0) return;

                var process = Process.GetProcessById((int)processId);
                OnAppChanged(process);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppUsageTracker] Initial focus synthesis failed: {ex.Message}");
            }
        }

        public void Stop()
        {
            lock (_sessionLock)
            {
                _periodicSaveTimer.Stop();

                if (_currentSession != null)
                {
                    _currentSession.EndTime = Clock();
                    SaveSessionToDb(_currentSession);
                    _currentSession = null;
                }

                _focusListener.Stop();
            }
        }

        /// <summary>
        /// Periodically saves the current session to prevent data loss on crash.
        /// Saves a completed segment and starts a new one for the same app.
        /// </summary>
        private void OnPeriodicSave(object? sender, System.Timers.ElapsedEventArgs e)
        {
            lock (_sessionLock)
            {
                CheckDayRollover();

                // This timer already ticks at a reasonable ~60s cadence, so no extra throttling
                // is needed here (unlike ScreenTimeTracker's 1s tick).
                TrackingHealthService.RecordHeartbeat(nameof(AppUsageTracker));

                var now = Clock();

                if ((now - _lastSaved).TotalMinutes < SaveIntervalMinutes)
                    return;

                if (_currentSession == null)
                    return;

                // End session when user is idle (same 300s threshold as ScreenTimeTracker)
                if (WindowsIdleTimeHelper.IsUserIdle(300))
                {
                    var duration = now - _currentSession.StartTime;
                    if (duration.TotalSeconds >= 30)
                    {
                        _currentSession.EndTime = now;
                        SaveSessionToDb(_currentSession);
                    }
                    _currentSession = null;
                    _lastSaved = now;
                    return;
                }

                var sessionDuration = now - _currentSession.StartTime;
                if (sessionDuration.TotalSeconds < 30)
                    return;

                _currentSession.EndTime = now;
                SaveSessionToDb(_currentSession);

                _currentSession = new AppUsageSession
                {
                    AppName = _currentSession.AppName,
                    ExecutablePath = _currentSession.ExecutablePath,
                    WindowTitle = _currentSession.WindowTitle,
                    StartTime = now
                };

                _lastSaved = now;
            }
        }

        /// <summary>
        /// Routine-path day-boundary guard - unlike FlushCurrentSession (only ever called from the
        /// explicit SystemEvents.TimeChanged handler), this runs on every periodic save and app
        /// switch, so a session left open overnight (same app, no clock change, no switch) still
        /// gets split at the day boundary the next time either fires. Matches
        /// ScreenTimeTracker.CheckDayRollover's granularity: detected within one save/switch tick
        /// of actual midnight, not sliced to the second - consistent with the existing template.
        /// </summary>
        private void CheckDayRollover()
        {
            if (_currentSession == null) return;

            var now = Clock();
            if (_currentSession.StartTime.Date == now.Date) return;

            // Normal forward rollover: cut at exact local midnight so neither day's totals gain
            // minutes from the other. Loop defensively for a machine that resumes after >1 day.
            while (_currentSession.StartTime.Date < now.Date)
            {
                var boundary = _currentSession.StartTime.Date.AddDays(1);
                _currentSession.EndTime = boundary;
                SaveSessionToDb(_currentSession);
                _currentSession = new AppUsageSession
                {
                    AppName = _currentSession.AppName,
                    ExecutablePath = _currentSession.ExecutablePath,
                    WindowTitle = _currentSession.WindowTitle,
                    StartTime = boundary
                };
            }

            // Backward clock/time-zone jumps cannot be split forward; restart at the new clock
            // and let the central interval validator reject the reversed prior interval.
            if (_currentSession.StartTime.Date > now.Date)
                FlushCurrentSession();
        }

        /// <summary>
        /// Force-close the current app segment and restart a fresh one for the same app at the new
        /// wall-clock time. Called on a system clock/timezone change so a single session cannot
        /// straddle two local-date buckets. Reversed/oversized intervals from a backward jump are
        /// rejected by DatabaseService, so no corrupt row is written.
        /// </summary>
        public void FlushCurrentSession()
        {
            lock (_sessionLock)
            {
                if (_currentSession == null) return;

                var now = Clock();
                _currentSession.EndTime = now;
                SaveSessionToDb(_currentSession);

                _currentSession = new AppUsageSession
                {
                    AppName = _currentSession.AppName,
                    ExecutablePath = _currentSession.ExecutablePath,
                    WindowTitle = _currentSession.WindowTitle,
                    StartTime = now
                };
                _lastSaved = now;
            }
        }

        private void OnAppChanged(Process? process)
        {
            bool shouldNotify = false;

            lock (_sessionLock)
            {
                CheckDayRollover();

                if (WindowsIdleTimeHelper.IsUserIdle(300))
                {
                    process?.Dispose();
                    return;
                }

                var now = Clock();

                if (_currentSession != null)
                {
                    _currentSession.EndTime = now;
                    SaveSessionToDb(_currentSession);
                    _currentSession = null;
                }

                if (process == null) return;

                try
                {
                    var appName = process.ProcessName;
                    var windowTitle = SafeGetWindowTitle(process);

                    // UWP apps run under ApplicationFrameHost - use window title as app name
                    if (string.Equals(appName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(windowTitle))
                    {
                        appName = windowTitle;
                    }

                    _currentSession = new AppUsageSession
                    {
                        AppName = appName,
                        ExecutablePath = SafeGetPath(process),
                        WindowTitle = windowTitle,
                        StartTime = now
                    };
                    shouldNotify = true;
                }
                finally
                {
                    process.Dispose();
                }
            }

            if (shouldNotify)
                OnAppSwitched?.Invoke();
        }

        private static string SafeGetPath(Process proc)
        {
            try { return proc.MainModule?.FileName ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string SafeGetWindowTitle(Process proc)
        {
            try { return proc.MainWindowTitle; }
            catch { return string.Empty; }
        }

        /// <summary>
        /// Saves a session to the DB without persisting the window title.
        /// Window titles can contain sensitive info (email subjects, passwords, URLs).
        /// The title is kept in memory for live display but not stored permanently.
        /// </summary>
        private static void SaveSessionToDb(AppUsageSession session)
        {
            var dbSession = new AppUsageSession
            {
                AppName = session.AppName,
                ExecutablePath = session.ExecutablePath,
                WindowTitle = null,  // Privacy: don't persist window titles
                StartTime = session.StartTime,
                EndTime = session.EndTime
            };
            DatabaseService.SaveAppUsageSession(dbSession);
        }

        public void Dispose()
        {
            _periodicSaveTimer.Stop();
            _periodicSaveTimer.Elapsed -= OnPeriodicSave;
            _periodicSaveTimer.Dispose();
            _focusListener.Stop();
        }
    }
}
