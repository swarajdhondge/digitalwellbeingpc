using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using digital_wellbeing_app.CoreLogic;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.ViewModels
{
    public class SoundTimelineViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // 0–23 markers
        public ObservableCollection<int> HourMarkers { get; }
            = new ObservableCollection<int>(Enumerable.Range(0, 24));

        public ObservableCollection<TimelineBar> Bars { get; }
            = new ObservableCollection<TimelineBar>();

        public ObservableCollection<SoundUsageSession> Sessions { get; }
            = new ObservableCollection<SoundUsageSession>();

        public ObservableCollection<SoundUsageSession> RecentSessions { get; }
            = new ObservableCollection<SoundUsageSession>();

        private int _totalSessionCount;
        private string _sessionSummaryText = string.Empty;

        public int TotalSessionCount
        {
            get => _totalSessionCount;
            private set
            {
                _totalSessionCount = value;
                OnPropertyChanged(nameof(TotalSessionCount));
            }
        }

        public string SessionSummaryText
        {
            get => _sessionSummaryText;
            private set
            {
                _sessionSummaryText = value;
                OnPropertyChanged(nameof(SessionSummaryText));
            }
        }

        private const int MaxRecentSessions = 10;

        private readonly SoundExposureManager _exposureMgr;
        private readonly DispatcherTimer _timer;
        private readonly SettingsService _settingsService = new();

        private string _thresholdLabel = string.Empty;
        public string ThresholdLabel
        {
            get => _thresholdLabel;
            private set
            {
                _thresholdLabel = value;
                OnPropertyChanged(nameof(ThresholdLabel));
            }
        }

        private TimeSpan _listening;
        public TimeSpan TotalListeningTime
        {
            get => _listening;
            private set
            {
                _listening = value;
                OnPropertyChanged(nameof(TotalListeningText));
            }
        }

        private TimeSpan _harmful;
        public TimeSpan TotalHarmfulTime
        {
            get => _harmful;
            private set
            {
                _harmful = value;
                OnPropertyChanged(nameof(TotalHarmfulText));
            }
        }

        public string TotalListeningText =>
            $"{(int)TotalListeningTime.TotalHours:D2}:{TotalListeningTime.Minutes:D2}:{TotalListeningTime.Seconds:D2}";
        public string TotalHarmfulText =>
            $"{(int)TotalHarmfulTime.TotalHours:D2}:{TotalHarmfulTime.Minutes:D2}:{TotalHarmfulTime.Seconds:D2}";

        public SoundTimelineViewModel()
        {
            // fully qualified WPF Application.Current
            _exposureMgr = (System.Windows.Application.Current as App)?.SoundExposureMgr
                           ?? new SoundExposureManager();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, __) => RefreshData();
            _timer.Start();

            RefreshData();
        }

        public void RefreshData()
        {
            // Load threshold from settings
            var threshold = _settingsService.LoadHarmfulThreshold();
            ThresholdLabel = $"Above {(int)threshold} dB";

            Bars.Clear();
            Sessions.Clear();
            RecentSessions.Clear();
            TotalListeningTime = TimeSpan.Zero;
            TotalHarmfulTime = TimeSpan.Zero;

            var today = DateTime.Today;
            var daySecs = TimeSpan.FromHours(24).TotalSeconds;

            // persisted sessions
            var saved = DatabaseService
                .GetSoundSessionsForDate(DateTime.Now)
                .OrderBy(s => s.StartTime)
                .ToList();

            foreach (var s in saved)
            {
                Sessions.Add(s);
                // Use ActualListeningDuration instead of wall-clock time
                TotalListeningTime += s.ActualListeningDuration;
                TotalHarmfulTime += s.HarmfulDuration;

                double startOff = Math.Clamp((s.StartTime - today).TotalSeconds, 0, daySecs);
                double endOff = Math.Clamp((s.EndTime - today).TotalSeconds, 0, daySecs);

                // Only add bar if there was actual listening
                if (s.ActualListeningDuration.TotalSeconds > 0)
                {
                    Bars.Add(new TimelineBar
                    {
                        StartFrac = startOff / daySecs,
                        EndFrac = endOff / daySecs,
                        HarmfulFrac = s.ActualListeningDuration.TotalSeconds > 0
                            ? Math.Min(1.0, s.HarmfulDuration.TotalSeconds / s.ActualListeningDuration.TotalSeconds)
                            : 0.0,
                        DeviceName = s.DeviceName,
                        SessionLabel = $"{s.StartTime:HH:mm}–{s.EndTime:HH:mm}"
                    });
                }
            }

            // live session
            var live = _exposureMgr.CurrentSession;
            if (live != null && live.ActualListeningDuration.TotalSeconds > 0)
            {
                var now = DateTime.Now;
                // Use ActualListeningDuration for live session too
                TotalListeningTime += live.ActualListeningDuration;
                TotalHarmfulTime += live.HarmfulDuration;

                double startOff = Math.Clamp((live.StartTime - today).TotalSeconds, 0, daySecs);
                double endOff = Math.Clamp((now - today).TotalSeconds, 0, daySecs);

                Bars.Add(new TimelineBar
                {
                    StartFrac = startOff / daySecs,
                    EndFrac = endOff / daySecs,
                    HarmfulFrac = live.ActualListeningDuration.TotalSeconds > 0
                        ? Math.Min(1.0, live.HarmfulDuration.TotalSeconds / live.ActualListeningDuration.TotalSeconds)
                        : 0.0,
                    DeviceName = live.DeviceName,
                    SessionLabel = $"{live.StartTime:HH:mm}–{now:HH:mm}"
                });
            }

            // Populate RecentSessions with most recent (reversed order, newest first)
            TotalSessionCount = Sessions.Count;
            var recentList = Sessions.OrderByDescending(s => s.StartTime).Take(MaxRecentSessions);
            foreach (var s in recentList)
            {
                RecentSessions.Add(s);
            }

            // Update summary text
            if (TotalSessionCount == 0)
            {
                SessionSummaryText = "No sessions today";
            }
            else if (TotalSessionCount <= MaxRecentSessions)
            {
                SessionSummaryText = $"{TotalSessionCount} session{(TotalSessionCount == 1 ? "" : "s")} today";
            }
            else
            {
                SessionSummaryText = $"Showing {MaxRecentSessions} most recent of {TotalSessionCount} sessions";
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            GC.SuppressFinalize(this);
        }
    }

    public class TimelineBar
    {
        public double StartFrac { get; set; }
        public double EndFrac { get; set; }
        public double HarmfulFrac { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string SessionLabel { get; set; } = string.Empty;
    }
}
