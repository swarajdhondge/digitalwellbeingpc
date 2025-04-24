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

        public AppUsageTracker()
        {
            _focusListener = new FocusChangeListener(OnAppChanged);
        }

        public void Start() => _focusListener.Start();
        public void Stop() => _focusListener.Stop();

        private void OnAppChanged(Process? process)
        {
            // If user is idle, skip
            if (WindowsIdleTimeHelper.IsUserIdle(60))
                return;

            var now = DateTime.Now;

            // End old session
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
        }

        private static string SafeGetPath(Process proc)
        {
            try
            {
                return proc.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string SafeGetWindowTitle(Process proc)
        {
            try
            {
                return proc.MainWindowTitle;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
