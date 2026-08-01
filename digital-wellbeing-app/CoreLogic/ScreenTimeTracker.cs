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

        // Throttles the tracking-health heartbeat write - _timer ticks every 1s, far too often to
        // write on every tick.
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
        private DateTime _lastHeartbeatUtc = DateTime.MinValue;

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

        /// <summary>
        /// Source of "now" for every day-boundary/segment/save decision after construction.
        /// Defaults to the real clock; overridable so long-running simulation tests can drive
        /// multi-day scenarios in seconds. Mirrors IdleTimeProvider's pattern: an instance
        /// property set after construction (e.g. between `new ScreenTimeTracker()` and
        /// `Start()`), not a constructor parameter. The constructor's own initial field values
        /// intentionally still use the real DateTime.Now - they run before any caller has a
        /// chance to override this property, so they anchor to "the real moment this tracker was
        /// created"; simulations advance forward from there via Start()/CheckDayRollover()/etc.
        /// </summary>
        public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

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
                _currentSegmentStart = Clock();
                _currentSegmentAccumulated = 0;
                _continuousSessionStart = Clock();
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

                _currentSegmentStart = Clock();
                _currentSegmentAccumulated = 0;
                _continuousSessionStart = Clock();
                _continuousSessionSeconds = 0;
                _sessionCount++;
                _state = TrackingState.Active;
                _idleStartTime = null;

                _timer.Start();
            }
            StateChanged?.Invoke(this, TrackingState.Active);
        }

        /// <summary>
        /// Force-flush persisted data and restart the current segment at the new wall-clock time.
        /// Called on a system clock/timezone change so day buckets stay coherent. Screen-time
        /// buckets are keyed by local date (yyyy-MM-dd); a mid-session clock jump would otherwise
        /// split one activity across days between the save-time and query-time keys.
        /// </summary>
        public void HandleTimeChanged()
        {
            lock (_stateLock)
            {
                if (_state == TrackingState.Active)
                {
                    // Persist the counters into the day that just ended. Using Clock() here
                    // would address the new day and drop the final unsaved interval from the
                    // previous day.
                    var activeDateKey = _sessionStartTime.ToString("yyyy-MM-dd");
                    SaveSessionData(activeDateKey);
                    SaveCurrentScreenSession(activeDateKey);
                }

                CheckDayRollover();

                var now = Clock();
                _currentSegmentStart = now;
                _currentSegmentAccumulated = 0;
                _continuousSessionStart = now;
                _continuousSessionSeconds = 0;
                _lastSaved = now;
            }
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

                var utcNow = DateTime.UtcNow;
                if (utcNow - _lastHeartbeatUtc >= HeartbeatInterval)
                {
                    _lastHeartbeatUtc = utcNow;
                    TrackingHealthService.RecordHeartbeat(nameof(ScreenTimeTracker));
                }

                // Get current idle state
                var idleTime = IdleTimeProvider();
                bool isUserIdle = idleTime.TotalSeconds > IdleThresholdSeconds;
                bool isPassivelyConsuming = ActivityDetector.IsPassivelyConsuming();

                bool shouldPauseTracking = isUserIdle && !isPassivelyConsuming;

                if (shouldPauseTracking)
                {
                    if (_state != TrackingState.Idle)
                    {
                        _idleStartTime ??= Clock();

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
                        _currentSegmentStart = Clock();
                        _currentSegmentAccumulated = 0;
                        _continuousSessionStart = Clock();
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
                var now = Clock();
                if ((now - _lastSaved).TotalMinutes >= SaveIntervalMinutes)
                {
                    SaveSessionData();

                    if (_currentSegmentAccumulated >= 30)
                    {
                        SaveCurrentScreenSession();
                        _currentSegmentStart = Clock();
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
                var todayKey = Clock().ToString("yyyy-MM-dd");
                var sessionDateKey = _sessionStartTime.ToString("yyyy-MM-dd");

                if (todayKey != sessionDateKey)
                {
                    // Persist the counters into the day that just ended. Using Clock() here
                    // would address the new day and drop the final unsaved interval from the
                    // previous day.
                    SaveSessionData(sessionDateKey);
                    SaveCurrentScreenSession(sessionDateKey);

                    // Reset for new day
                    _activeTime = TimeSpan.Zero;
                    _sessionStartTime = Clock();
                    _currentSegmentStart = Clock();
                    _currentSegmentAccumulated = 0;
                    _continuousSessionStart = Clock();
                    _continuousSessionSeconds = 0;
                    _sessionCount = 1;

                    // Create new day entry - find-then-update-or-insert rather than a bare
                    // InsertOrReplace(entry) on a freshly-constructed object. ScreenTimePeriod's
                    // PK is an AutoIncrement Id, defaulted to 0 on every new instance here;
                    // InsertOrReplace includes that explicit Id=0 in its SQL, so every rollover
                    // after the first one *replaced* the same Id=0 row instead of inserting a new
                    // one - only the most recent day's summary ever survived on a machine that
                    // stayed running across multiple real midnights without restarting. Found via
                    // Item 10's long-running simulation test, not previously covered by any
                    // existing (short-lived, single-process-run) test.
                    var db = DatabaseService.GetConnection();
                    var existingEntry = db.Table<ScreenTimePeriod>().FirstOrDefault(x => x.SessionDate == todayKey);
                    if (existingEntry != null)
                    {
                        // Race guard: another path (e.g. LoadSessionData at a near-simultaneous
                        // startup) already created today's row - update it in place instead of
                        // inserting a duplicate.
                        existingEntry.SessionStartTime = _sessionStartTime.ToString("o");
                        existingEntry.LastRecordedTime = Clock().ToString("o");
                        existingEntry.AccumulatedActiveSeconds = 0;
                        db.Update(existingEntry);
                    }
                    else
                    {
                        var entry = new ScreenTimePeriod
                        {
                            SessionDate = todayKey,
                            SessionStartTime = _sessionStartTime.ToString("o"),
                            LastRecordedTime = Clock().ToString("o"),
                            AccumulatedActiveSeconds = 0
                        };
                        db.Insert(entry);
                    }

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
                // Today's period can't have started before today began — a machine with multi-day
                // uptime would otherwise stamp today's session with a prior date. Clamp to midnight.
                if (bootTime < DateTime.Today)
                    bootTime = DateTime.Today;

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
                // Tolerate a missing/corrupt SessionStartTime (e.g. a legacy or partially-written
                // row) instead of throwing on startup — fall back to "now".
                if (!DateTime.TryParse(entry.SessionStartTime, out var start))
                    start = DateTime.Now;
                var active = TimeSpan.FromSeconds(entry.AccumulatedActiveSeconds);
                return (active, start, sessions > 0 ? sessions : 1);
            }
        }

        private void SaveSessionData(string? dateKey = null)
        {
            var db = DatabaseService.GetConnection();
            var todayKey = dateKey ?? Clock().ToString("yyyy-MM-dd");
            var entry = db.Table<ScreenTimePeriod>()
                          .FirstOrDefault(x => x.SessionDate == todayKey);
            if (entry == null) return;

            entry.AccumulatedActiveSeconds = (int)_activeTime.TotalSeconds;
            entry.LastRecordedTime = Clock().ToString("o");
            db.Update(entry);
        }

        /// <summary>
        /// Save a session segment for timeline visualization
        /// </summary>
        private void SaveCurrentScreenSession(string? dateKey = null)
        {
            if (_currentSegmentStart == null || _currentSegmentAccumulated < 1)
                return;

            // Skip very short segments (less than 30 seconds)
            if (_currentSegmentAccumulated < 30)
                return;

            var session = new ScreenTimeSession
            {
                SessionDate = dateKey ?? Clock().ToString("yyyy-MM-dd"),
                StartTime = _currentSegmentStart.Value,
                DurationSeconds = _currentSegmentAccumulated
            };

            DatabaseService.SaveScreenTimeSession(session);

            // Reset segment for next save (continuous session keeps running)
            _currentSegmentStart = Clock();
            _currentSegmentAccumulated = 0;
        }
    }
}
