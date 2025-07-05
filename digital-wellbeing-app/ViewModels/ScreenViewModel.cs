using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;
using digital_wellbeing_app.CoreLogic;

namespace digital_wellbeing_app.ViewModels
{
    public class ScreenViewModel : INotifyPropertyChanged, IDisposable
    {
        public ObservableCollection<WeeklyUsageItem> WeeklyUsage { get; } = [];
        public ObservableCollection<ScreenTimelineSegment> TimelineSegments { get; } = [];


        public ObservableCollection<int> HourMarkers { get; } =
            new ObservableCollection<int>(Enumerable.Range(0, 24));

        private const double WindowSeconds = 24 * 60 * 60; // 12 hours

        private string _todayTimeText = "00:00:00";
        public string TodayTimeText
        {
            get => _todayTimeText;
            set
            {
                if (_todayTimeText == value) return;
                _todayTimeText = value;
                OnPropertyChanged(nameof(TodayTimeText));
            }
        }

        private readonly ScreenTimeTracker _tracker;
        private readonly DispatcherTimer _timer;
        private bool _disposed;

        public ScreenViewModel()
        {
            _tracker = (System.Windows.Application.Current as App)?.ScreenTracker
                       ?? new ScreenTimeTracker();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateTodayUsage();
            _timer.Start();

            LoadWeeklyUsage();
            UpdateTodayUsage();
        }

        public void LoadWeeklyUsage()
        {
            if (_disposed) return;
            WeeklyUsage.Clear();
            var db = DatabaseService.GetConnection();

            DateTime monday = DateTime.Today;
            while (monday.DayOfWeek != DayOfWeek.Monday)
                monday = monday.AddDays(-1);

            for (int i = 0; i < 7; i++)
            {
                var day = monday.AddDays(i);
                var key = day.ToString("yyyy-MM-dd");
                var entry = db.Table<ScreenTimePeriod>()
                              .FirstOrDefault(x => x.SessionDate == key);
                int sec = entry?.AccumulatedActiveSeconds ?? 0;
                var ts = TimeSpan.FromSeconds(sec);

                WeeklyUsage.Add(new WeeklyUsageItem
                {
                    Day = day.DayOfWeek.ToString(),
                    Usage = $"{(int)ts.TotalHours} hr {ts.Minutes} min"
                });
            }
        }

        private void UpdateTodayUsage()
        {
            if (_disposed) return;

            var ts = _tracker.CurrentActiveTime;
            int totalSec = (int)ts.TotalSeconds;
            TodayTimeText =
                $"{totalSec / 3600} hr {(totalSec % 3600) / 60} min {totalSec % 60} sec";

            UpdateTimelineSegments();
        }

        private void UpdateTimelineSegments()
        {
            TimelineSegments.Clear();
            var db = DatabaseService.GetConnection();
            var todayKey = DateTime.Today.ToString("yyyy-MM-dd");

            // 1) Saved session segments (if you have them)
            var sessions = db.Table<ScreenTimeSession>()
                             .Where(x => x.SessionDate == todayKey)
                             .ToList();

            const double daySecs = 24 * 60 * 60;
            foreach (var s in sessions)
            {
                double start = s.StartTime.TimeOfDay.TotalSeconds;
                double dur = s.DurationSeconds;
                if (dur <= 0) continue;
                TimelineSegments.Add(new ScreenTimelineSegment
                {
                    StartPercent = start / daySecs,
                    WidthPercent = Math.Min(1.0, dur / daySecs)
                });
            }

            // 2) Live session segment – compute its start as (now - duration)
            var activeSeconds = _tracker.CurrentActiveTime.TotalSeconds;
            if (activeSeconds > 0)
            {
                var nowSecs = (DateTime.Now - DateTime.Today).TotalSeconds;
                var startSecs = Math.Max(0, nowSecs - activeSeconds);
                var widthSecs = activeSeconds;

                TimelineSegments.Add(new ScreenTimelineSegment
                {
                    StartPercent = startSecs / daySecs,
                    WidthPercent = Math.Min(1.0, widthSecs / daySecs)
                });
            }

            OnPropertyChanged(nameof(TimelineSegments));
        }


        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            GC.SuppressFinalize(this);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class WeeklyUsageItem
    {
        public string Day { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
    }
}
