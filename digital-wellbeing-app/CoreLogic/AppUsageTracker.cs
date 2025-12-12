using System;
using System.Diagnostics;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Platform.Windows;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.CoreLogic
{
    public class AppUsageTracker
    {
        private AppUsageSession? _currentSession;
        private readonly FocusChangeListener _focusListener;
        private readonly System.Timers.Timer _periodicSaveTimer;
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
            _periodicSaveTimer.Stop();

            // End any in‐flight session
            if (_currentSession != null)
            {
                _currentSession.EndTime = DateTime.Now;
                DatabaseService.SaveAppUsageSession(_currentSession);
                _currentSession = null;
            }

            _focusListener.Stop();
        }

        /// <summary>
        /// Periodically saves the current session to prevent data loss on crash.
        /// Saves a completed segment and starts a new one for the same app.
        /// </summary>
        private void OnPeriodicSave(object? sender, System.Timers.ElapsedEventArgs e)
        {
            var now = DateTime.Now;

            // Only save every SaveIntervalMinutes
            if ((now - _lastSaved).TotalMinutes < SaveIntervalMinutes)
                return;

            if (_currentSession == null)
                return;

            // Skip very short sessions (less than 30 seconds)
            var duration = now - _currentSession.StartTime;
            if (duration.TotalSeconds < 30)
                return;

            // Save the current session segment
            _currentSession.EndTime = now;
            DatabaseService.SaveAppUsageSession(_currentSession);

            // Start a new session segment for the same app (seamless continuation)
            _currentSession = new AppUsageSession
            {
                AppName = _currentSession.AppName,
                ExecutablePath = _currentSession.ExecutablePath,
                WindowTitle = _currentSession.WindowTitle,
                StartTime = now
            };

            _lastSaved = now;
        }

        private void OnAppChanged(Process? process)
        {
            // If user is idle, skip
            if (WindowsIdleTimeHelper.IsUserIdle(60))
                return;

            var now = DateTime.Now;

            // End previous session if exists
            if (_currentSession != null)
            {
                _currentSession.EndTime = now;
                DatabaseService.SaveAppUsageSession(_currentSession);
                _currentSession = null;
            }

            if (process == null) return;

            // Start new usage session
            _currentSession = new AppUsageSession
            {
                AppName = process.ProcessName,
                ExecutablePath = SafeGetPath(process),
                WindowTitle = SafeGetWindowTitle(process),
                StartTime = now
            };

            // Notify subscribers
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
    }
}
