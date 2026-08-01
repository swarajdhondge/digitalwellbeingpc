using System;
using System.Diagnostics;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Platform.Windows;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Opt-in (default off - see SettingsService.LoadWebsiteTrackingEnabled) low-frequency poll of
    /// the foreground window's address bar, reduced to a bare hostname. Needs its own poll timer -
    /// tab switches within a browser don't fire the EVENT_SYSTEM_FOREGROUND AppUsageTracker
    /// listens for, so there's no existing event to piggyback on.
    /// </summary>
    public class WebsiteUsageService : IDisposable
    {
        private const int PollIntervalSeconds = 7; // within the recommended 5-10s range
        private const int CheckpointIntervalMinutes = 5; // matches AppUsageTracker.SaveIntervalMinutes

        private readonly System.Timers.Timer _pollTimer;
        private readonly object _sessionLock = new();
        private WebsiteUsageSession? _currentSession;
        private DateTime _lastCheckpoint = DateTime.Now;
        private bool _isDisposed;

        public bool IsRunning { get; private set; }
        public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

        public WebsiteUsageService()
        {
            _pollTimer = new System.Timers.Timer(PollIntervalSeconds * 1000) { AutoReset = true };
            _pollTimer.Elapsed += OnPoll;
        }

        public void Start()
        {
            if (IsRunning) return;
            _pollTimer.Start();
            IsRunning = true;
        }

        public void Stop()
        {
            if (!IsRunning) return;
            lock (_sessionLock)
            {
                _pollTimer.Stop();
                EndCurrentSessionLocked(Clock());
            }
            IsRunning = false;
        }

        /// <summary>Applies a live toggle from Settings without needing an app restart.</summary>
        public void SetEnabled(bool enabled)
        {
            if (enabled) Start();
            else Stop();
        }

        private void OnPoll(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                string? processName = null;
                IntPtr handle = NativeMethods.GetForegroundWindow();

                if (handle != IntPtr.Zero)
                {
                    NativeMethods.GetWindowThreadProcessId(handle, out uint pid);
                    if (pid != 0)
                    {
                        using var process = Process.GetProcessById((int)pid);
                        processName = process.ProcessName;
                    }
                }

                string? hostname = null;
                if (processName != null && BrowserTabInspector.IsKnownBrowser(processName))
                {
                    var rawText = BrowserTabInspector.TryGetAddressBarText(handle, processName);
                    hostname = BrowserTabInspector.ExtractHostname(rawText);
                }

                lock (_sessionLock)
                {
                    var now = Clock();

                    // Keep date-bucket queries exact across midnight. A five-minute checkpoint
                    // can otherwise leave the first minutes of a new day attached to yesterday.
                    SplitAtDayBoundaryLocked(now);

                    if (hostname == null || processName == null)
                    {
                        // Not a known browser, or the tab isn't a real http(s) page this tick -
                        // close whatever was open. At a 7s poll granularity, a session resuming a
                        // tick later just starts fresh, which is an acceptable v1 tradeoff.
                        EndCurrentSessionLocked(now);
                        return;
                    }

                    if (_currentSession != null &&
                        string.Equals(_currentSession.Hostname, hostname, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(_currentSession.BrowserProcessName, processName, StringComparison.OrdinalIgnoreCase))
                    {
                        _currentSession.EndTime = now;

                        if ((now - _lastCheckpoint).TotalMinutes >= CheckpointIntervalMinutes)
                        {
                            // Crash-safety checkpoint: close this segment, open a fresh one for the
                            // same site - mirrors AppUsageTracker.OnPeriodicSave.
                            SaveSessionToDb(_currentSession);
                            _currentSession = NewSession(processName, hostname, now);
                            _lastCheckpoint = now;
                        }
                        return;
                    }

                    // Site or browser changed - close the prior segment, open a new one.
                    EndCurrentSessionLocked(now);
                    _currentSession = NewSession(processName, hostname, now);
                    _lastCheckpoint = now;
                }
            }
            catch (Exception ex)
            {
                LogService.Warning($"WebsiteUsageService poll failed: {ex.Message}");
            }
        }

        private static WebsiteUsageSession NewSession(string processName, string hostname, DateTime now) => new()
        {
            BrowserProcessName = processName,
            Hostname = hostname,
            StartTime = now,
            EndTime = now
        };

        private void EndCurrentSessionLocked(DateTime now)
        {
            if (_currentSession == null) return;

            _currentSession.EndTime = now;
            // Filter noise shorter than one poll tick (e.g. glancing at the tab bar).
            if ((_currentSession.EndTime - _currentSession.StartTime).TotalSeconds >= PollIntervalSeconds)
                SaveSessionToDb(_currentSession);

            _currentSession = null;
        }

        private void SplitAtDayBoundaryLocked(DateTime now)
        {
            if (_currentSession == null || _currentSession.StartTime.Date == now.Date) return;

            var prior = _currentSession;
            var boundary = now.Date;
            prior.EndTime = boundary;
            if ((prior.EndTime - prior.StartTime).TotalSeconds >= PollIntervalSeconds)
                SaveSessionToDb(prior);

            _currentSession = NewSession(prior.BrowserProcessName, prior.Hostname, boundary);
            _currentSession.EndTime = now;
            _lastCheckpoint = boundary;
        }

        private static void SaveSessionToDb(WebsiteUsageSession session)
        {
            DatabaseService.SaveWebsiteUsageSession(new WebsiteUsageSession
            {
                BrowserProcessName = session.BrowserProcessName,
                Hostname = session.Hostname,
                StartTime = session.StartTime,
                EndTime = session.EndTime
            });
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            Stop();
            _pollTimer.Dispose();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
