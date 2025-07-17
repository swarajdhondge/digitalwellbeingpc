using System;
using System.Timers;
using digital_wellbeing_app.Platform.Windows;
using digital_wellbeing_app.Resources;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.CoreLogic
{
    public class ScreenTimeTracker
    {
        private readonly System.Timers.Timer _timer;
        private TimeSpan _activeTime;
        private DateTime _sessionStartTime;
        private DateTime _lastSaved;

        // --- For session tracking ---
        private DateTime? _currentSessionStart = null;
        private int _currentSessionAccumulated = 0;

        public TimeSpan CurrentActiveTime => _activeTime;
        public DateTime SessionStartTime => _sessionStartTime;

        public ScreenTimeTracker()
        {
            var (initialActive, start) = LoadSessionData();
            _activeTime = initialActive;
            _sessionStartTime = start;
            _lastSaved = DateTime.Now;

            // Initialize session tracking
            _currentSessionStart = DateTime.Now;
            _currentSessionAccumulated = 0;

            _timer = new System.Timers.Timer(1_000)
            {
                AutoReset = true
            };
            _timer.Elapsed += CheckActivity;
        }

        public void Start()
        {
            // New session starts now
            _currentSessionStart = DateTime.Now;
            _currentSessionAccumulated = 0;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            SaveSessionData();
            SaveCurrentScreenSession(); // Save the last session segment
        }

        private void CheckActivity(object? sender, ElapsedEventArgs e)
        {
            _activeTime = _activeTime.Add(TimeSpan.FromSeconds(1));
            _currentSessionAccumulated += 1; // Track this session's seconds

            var now = DateTime.Now;
            // Save every 15 mins as before
            if ((now - _lastSaved).TotalMinutes >= 15)
            {
                SaveSessionData();
                SaveCurrentScreenSession(); // Save current session every 15 min as a chunk
                _currentSessionStart = DateTime.Now;
                _currentSessionAccumulated = 0;
                _lastSaved = now;
            }
        }

        private static (TimeSpan initialActive, DateTime sessionStart) LoadSessionData()
        {
            var db = DatabaseService.GetConnection();
            var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
            var entry = db.Table<ScreenTimePeriod>()
                          .FirstOrDefault(x => x.SessionDate == todayKey);

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
                return (TimeSpan.Zero, bootTime);
            }
            else
            {
                var start = DateTime.Parse(entry.SessionStartTime);
                var active = TimeSpan.FromSeconds(entry.AccumulatedActiveSeconds);
                return (active, start);
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

        // --- New: Save a session segment for timeline visualization ---
        private void SaveCurrentScreenSession()
        {
            if (_currentSessionStart == null || _currentSessionAccumulated < 1)
                return;

            // You may skip saving if the segment is "too short" (<30 sec), up to you!
            if (_currentSessionAccumulated < 30)
            {
                // Discard short segments (adjust threshold as desired)
                return;
            }

            var session = new ScreenTimeSession
            {
                SessionDate = DateTime.Now.ToString("yyyy-MM-dd"),
                StartTime = _currentSessionStart.Value,
                DurationSeconds = _currentSessionAccumulated
            };

            DatabaseService.SaveScreenTimeSession(session);

            // Reset for next session (next Start/Stop or 15-min chunk)
            _currentSessionStart = DateTime.Now;
            _currentSessionAccumulated = 0;
        }
    }
}
