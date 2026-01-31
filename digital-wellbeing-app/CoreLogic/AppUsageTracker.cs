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
        }

        public void Stop()
        {
            lock (_sessionLock)
            {
                _periodicSaveTimer.Stop();

                if (_currentSession != null)
                {
                    _currentSession.EndTime = DateTime.Now;
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
                var now = DateTime.Now;

                if ((now - _lastSaved).TotalMinutes < SaveIntervalMinutes)
                    return;

                if (_currentSession == null)
                    return;

                var duration = now - _currentSession.StartTime;
                if (duration.TotalSeconds < 30)
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

        private void OnAppChanged(Process? process)
        {
            bool shouldNotify = false;

            lock (_sessionLock)
            {
                if (WindowsIdleTimeHelper.IsUserIdle(60))
                {
                    process?.Dispose();
                    return;
                }

                var now = DateTime.Now;

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
