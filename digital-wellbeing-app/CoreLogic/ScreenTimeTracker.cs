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

        public TimeSpan CurrentActiveTime => _activeTime;
        public DateTime SessionStartTime => _sessionStartTime;

        public ScreenTimeTracker()
        {
            var (initialActive, start) = LoadSessionData();
            _activeTime = initialActive;
            _sessionStartTime = start;
            _lastSaved = DateTime.Now;

            _timer = new System.Timers.Timer(1_000)  // 1 Hz tick
            {
                AutoReset = true
            };
            _timer.Elapsed += CheckActivity;
        }

        public void Start() => _timer.Start();

        public void Stop()
        {
            _timer.Stop();
            SaveSessionData();  // final flush on exit
        }

        private void CheckActivity(object? sender, ElapsedEventArgs e)
        {
            // **always** add one second per tick, no idle check
            _activeTime = _activeTime.Add(TimeSpan.FromSeconds(1));

            // every 15 min, persist
            var now = DateTime.Now;
            if ((now - _lastSaved).TotalMinutes >= 15)
            {
                SaveSessionData();
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
                // first run today → sessionStart = system boot time
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
    }
}
