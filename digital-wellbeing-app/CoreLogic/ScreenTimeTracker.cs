using System;
using System.Timers;
using digital_wellbeing_app.Platform.Windows;
using digital_wellbeing_app.Services;
using digital_wellbeing_app.Resources;

namespace digital_wellbeing_app.CoreLogic
{
    public class ScreenTimeTracker
    {
        private readonly System.Timers.Timer _timer;
        private TimeSpan _activeTime;
        private DateTime _lastSaved;
        private DateTime _sessionStartTime;

        public TimeSpan CurrentActiveTime => _activeTime + (DateTime.Now - _lastSaved);
        public DateTime SessionStartTime => _sessionStartTime;

        public ScreenTimeTracker()
        {
            _activeTime = LoadSessionData();
            _sessionStartTime = DateTime.Now;
            _lastSaved = DateTime.Now;

            _timer = new System.Timers.Timer(60000); // 1 minute
            _timer.Elapsed += CheckActivity;
        }

        public void Start() => _timer.Start();

        public void Stop()
        {
            _timer.Stop();
            SaveSessionData(); // Final write
        }

        private void CheckActivity(object? sender, ElapsedEventArgs e)
        {
            var idle = WindowsIdleTimeHelper.GetIdleTime();

            if (idle.TotalSeconds < Constants.IdleThresholdSeconds)
            {
                _activeTime = _activeTime.Add(TimeSpan.FromMinutes(1));
            }

            if ((DateTime.Now - _lastSaved).TotalMinutes >= 15)
            {
                SaveSessionData();
                _lastSaved = DateTime.Now;
            }
        }

        private static TimeSpan LoadSessionData()
        {
            var db = DatabaseService.GetConnection();
            var today = DateTime.Now.ToString("yyyy-MM-dd");

            var entry = db.Table<ScreenTimeSession>()
                          .FirstOrDefault(x => x.SessionDate == today);

            if (entry == null)
            {
                entry = new()
                {
                    SessionDate = today,
                    SessionStartTime = DateTime.Now.ToString("hh:mm tt"),
                    LastRecordedTime = DateTime.Now.ToString("hh:mm tt"),
                    AccumulatedActiveSeconds = 0
                };
                db.Insert(entry);
                return TimeSpan.Zero;
            }

            return TimeSpan.FromSeconds(entry.AccumulatedActiveSeconds);
        }

        private void SaveSessionData()
        {
            var db = DatabaseService.GetConnection();
            var today = DateTime.Now.ToString("yyyy-MM-dd");

            var entry = db.Table<ScreenTimeSession>()
                          .FirstOrDefault(x => x.SessionDate == today);

            if (entry != null)
            {
                entry.LastRecordedTime = DateTime.Now.ToString("hh:mm tt");
                entry.AccumulatedActiveSeconds = (int)_activeTime.TotalSeconds;
                db.Update(entry);
            }
        }
    }

    public class ScreenTimeSession
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }
        public string SessionDate { get; set; } = string.Empty;
        public string SessionStartTime { get; set; } = string.Empty;
        public string LastRecordedTime { get; set; } = string.Empty;
        public int AccumulatedActiveSeconds { get; set; }
    }
}
