using System;
using System.Timers;
using digital_wellbeing_app.Platform.Windows;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.CoreLogic
{
    /// <summary>
    /// Tracking states for screen time
    /// </summary>
    public enum TrackingState
    {
        Active,  // Actively counting time (user input detected)
        Idle,    // No activity but passive consumption possible (watching video)
        Paused   // Screen locked or PC sleeping
    }

    public class ScreenTimeTracker : IDisposable
    {
        private readonly System.Timers.Timer _timer;
        private readonly object _stateLock = new();
        private TimeSpan _activeTime;
        private DateTime _sessionStartTime;
        private DateTime _lastSaved;

        // Session segment tracking (for DB saves every 5 min)
        private DateTime? _currentSegmentStart = null;
        private int _currentSegmentAccumulated = 0;

        // Continuous session tracking (doesn't reset on 5-min saves)
        private DateTime? _continuousSessionStart = null;
        private int _continuousSessionSeconds = 0;

        // Idle detection settings
        private const int IdleThresholdSeconds = 300; // 5 minutes
        private const int SaveIntervalMinutes = 5;    // Save every 5 minutes (was 15)

        // State tracking
        private TrackingState _state = TrackingState.Active;
        private DateTime? _idleStartTime = null;
        private int _sessionCount = 0;

        // Public properties
        public TimeSpan CurrentActiveTime => _activeTime;
        public DateTime SessionStartTime => _sessionStartTime;
        
        /// <summary>Current tracking state (Active, Idle, or Paused)</summary>
        public TrackingState State => _state;
        
        /// <summary>When the current segment started (resets every 5 min save)</summary>
        public DateTime? CurrentSessionStart => _currentSegmentStart;
        
        /// <summary>Seconds accumulated in current segment (resets every 5 min)</summary>
        public int CurrentSessionSeconds => _currentSegmentAccumulated;

        /// <summary>When the current continuous session started (only resets on idle/pause)</summary>
        public DateTime? ContinuousSessionStart => _continuousSessionStart;

        /// <summary>Total seconds in current continuous session (only resets on idle/pause)</summary>
        public int ContinuousSessionSeconds => _continuousSessionSeconds;
        
        /// <summary>Number of sessions tracked today</summary>
        public int SessionCount => _sessionCount;

        /// <summary>Event fired when tracking state changes</summary>
        public event EventHandler<TrackingState>? StateChanged;

        /// <summary>
        /// Source of the user's idle duration. Defaults to the real Win32 helper; overridable so
        /// tests can drive the active/idle path deterministically. (Headless CI and idle machines
        /// have no recent input, which would otherwise make activity-based accumulation flaky.)
        /// </summary>
        public Func<TimeSpan> IdleTimeProvider { get; set; } = WindowsIdleTimeHelper.GetIdleTime;

        public ScreenTimeTracker()
        {
            var (initialActive, start, sessions) = LoadSessionData();
            _activeTime = initialActive;
            _sessionStartTime = start;
            _sessionCount = sessions;
            _lastSaved = DateTime.Now;

            // Initialize session tracking
            _currentSegmentStart = DateTime.Now;
            _currentSegmentAccumulated = 0;
            _continuousSessionStart = DateTime.Now;
            _continuousSessionSeconds = 0;

            _timer = new System.Timers.Timer(1_000)
            {
                AutoReset = true
            };
            _timer.Elapsed += CheckActivity;
        }

        public void Start()
        {
            lock (_stateLock)
            {
                _currentSegmentStart = DateTime.Now;
                _currentSegmentAccumulated = 0;
                _continuousSessionStart = DateTime.Now;
                _continuousSessionSeconds = 0;
                _state = TrackingState.Active;
                _timer.Start();
            }
        }

        public void Stop()
        {
            lock (_stateLock)
            {
                _timer.Stop();
                SaveSessionData();
                SaveCurrentScreenSession();
            }
        }

        /// <summary>
        /// Pause tracking (called on screen lock, sleep, etc.)
        /// </summary>
        public void Pause()
        {
            lock (_stateLock)
            {
                if (_state == TrackingState.Paused)
                    return;

                _timer.Stop();
                SaveSessionData();
                SaveCurrentScreenSession();

                _state = TrackingState.Paused;
            }
            StateChanged?.Invoke(this, TrackingState.Paused);
        }

        /// <summary>
        /// Resume tracking (called on screen unlock, wake, etc.)
        /// </summary>
        public void Resume()
        {
            lock (_stateLock)
            {
                if (_state != TrackingState.Paused)
                    return;

                CheckDayRollover();

                _currentSegmentStart = DateTime.Now;
                _currentSegmentAccumulated = 0;
                _continuousSessionStart = DateTime.Now;
                _continuousSessionSeconds = 0;
                _sessionCount++;
                _state = TrackingState.Active;
                _idleStartTime = null;

                _timer.Start();
            }
            StateChanged?.Invoke(this, TrackingState.Active);
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Elapsed -= CheckActivity;
            _timer.Dispose();
        }

        private void CheckActivity(object? sender, ElapsedEventArgs e)
        {
            TrackingState? stateChangeToReport = null;

            lock (_stateLock)
            {
                // Check for day rollover at midnight
                CheckDayRollover();

                // Get current idle state
                var idleTime = IdleTimeProvider();
                bool isUserIdle = idleTime.TotalSeconds > IdleThresholdSeconds;
                bool isPassivelyConsuming = ActivityDetector.IsPassivelyConsuming();

                bool shouldPauseTracking = isUserIdle && !isPassivelyConsuming;

                if (shouldPauseTracking)
                {
                    if (_state != TrackingState.Idle)
                    {
                        _idleStartTime ??= DateTime.Now;

                        if (_state == TrackingState.Active)
                        {
                            SaveSessionData();
                            SaveCurrentScreenSession();
                        }

                        _state = TrackingState.Idle;
                        stateChangeToReport = _state;
                    }
                }
                else
                {
                    if (_state == TrackingState.Idle)
                    {
                        _currentSegmentStart = DateTime.Now;
                        _currentSegmentAccumulated = 0;
                        _continuousSessionStart = DateTime.Now;
                        _continuousSessionSeconds = 0;
                        _sessionCount++;
                        _idleStartTime = null;
                        _state = TrackingState.Active;
                        stateChangeToReport = _state;
                    }

                    _activeTime = _activeTime.Add(TimeSpan.FromSeconds(1));
                    _currentSegmentAccumulated++;
                    _continuousSessionSeconds++;
                }

                // Periodic save
                var now = DateTime.Now;
                if ((now - _lastSaved).TotalMinutes >= SaveIntervalMinutes)
                {
                    SaveSessionData();

                    if (_currentSegmentAccumulated >= 30)
                    {
                        SaveCurrentScreenSession();
                        _currentSegmentStart = DateTime.Now;
                        _currentSegmentAccumulated = 0;
                    }

                    _lastSaved = now;
                }
            }

            // Fire events outside the lock to avoid potential deadlocks
            if (stateChangeToReport.HasValue)
            {
                StateChanged?.Invoke(this, stateChangeToReport.Value);
            }
        }

        private void CheckDayRollover()
        {
            try
            {
                var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
                var sessionDateKey = _sessionStartTime.ToString("yyyy-MM-dd");

                if (todayKey != sessionDateKey)
                {
                    // Day changed - save current session and reset
                    SaveSessionData();
                    SaveCurrentScreenSession();

                    // Reset for new day
                    _activeTime = TimeSpan.Zero;
                    _sessionStartTime = DateTime.Now;
                    _currentSegmentStart = DateTime.Now;
                    _currentSegmentAccumulated = 0;
                    _continuousSessionStart = DateTime.Now;
                    _continuousSessionSeconds = 0;
                    _sessionCount = 1;

                    // Create new day entry (InsertOrReplace handles race condition)
                    var db = DatabaseService.GetConnection();
                    var entry = new ScreenTimePeriod
                    {
                        SessionDate = todayKey,
                        SessionStartTime = _sessionStartTime.ToString("o"),
                        LastRecordedTime = DateTime.Now.ToString("o"),
                        AccumulatedActiveSeconds = 0
                    };
                    db.InsertOrReplace(entry);

                    System.Diagnostics.Debug.WriteLine($"[ScreenTimeTracker] Day rollover: {sessionDateKey} -> {todayKey}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenTimeTracker] CheckDayRollover error: {ex.Message}");
            }
        }

        private static (TimeSpan initialActive, DateTime sessionStart, int sessionCount) LoadSessionData()
        {
            var db = DatabaseService.GetConnection();
            var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
            var entry = db.Table<ScreenTimePeriod>()
                          .FirstOrDefault(x => x.SessionDate == todayKey);

            // Count existing sessions for today
            var sessions = db.Table<ScreenTimeSession>()
                             .Where(x => x.SessionDate == todayKey)
                             .Count();

            if (entry == null)
            {
                var bootTime = DateTime.Now
                             - TimeSpan.FromMilliseconds(Environment.TickCount64);

                entry = new ScreenTimePeriod
                {
                    SessionDate = todayKey,
                    SessionStartTime = bootTime.ToString("o"),
                    LastRecordedTime = DateTime.Now.ToString("o"),
                    AccumulatedActiveSeconds = 0
                };
                db.Insert(entry);
                return (TimeSpan.Zero, bootTime, sessions > 0 ? sessions : 1);
            }
            else
            {
                var start = DateTime.Parse(entry.SessionStartTime);
                var active = TimeSpan.FromSeconds(entry.AccumulatedActiveSeconds);
                return (active, start, sessions > 0 ? sessions : 1);
            }
        }

        private void SaveSessionData()
        {
            var db = DatabaseService.GetConnection();
            var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
            var entry = db.Table<ScreenTimePeriod>()
                          .FirstOrDefault(x => x.SessionDate == todayKey);
            if (entry == null) return;

            entry.AccumulatedActiveSeconds = (int)_activeTime.TotalSeconds;
            entry.LastRecordedTime = DateTime.Now.ToString("o");
            db.Update(entry);
        }

        /// <summary>
        /// Save a session segment for timeline visualization
        /// </summary>
        private void SaveCurrentScreenSession()
        {
            if (_currentSegmentStart == null || _currentSegmentAccumulated < 1)
                return;

            // Skip very short segments (less than 30 seconds)
            if (_currentSegmentAccumulated < 30)
                return;

            var session = new ScreenTimeSession
            {
                SessionDate = DateTime.Now.ToString("yyyy-MM-dd"),
                StartTime = _currentSegmentStart.Value,
                DurationSeconds = _currentSegmentAccumulated
            };

            DatabaseService.SaveScreenTimeSession(session);

            // Reset segment for next save (continuous session keeps running)
            _currentSegmentStart = DateTime.Now;
            _currentSegmentAccumulated = 0;
        }
    }
}
