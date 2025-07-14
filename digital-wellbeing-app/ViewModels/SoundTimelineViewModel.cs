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

        private readonly SoundExposureManager _exposureMgr;
        private readonly DispatcherTimer _timer;

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
            Bars.Clear();
            Sessions.Clear();
            TotalListeningTime = TimeSpan.Zero;
            TotalHarmfulTime = TimeSpan.Zero;

            var today = DateTime.Today;
            var daySecs = TimeSpan.FromHours(24).TotalSeconds;

            // persisted sessions
            var saved = DatabaseService
                .GetSoundSessionsForDate(DateTime.Now)
                .OrderBy(s => s.StartTime);
            foreach (var s in saved)
            {
                Sessions.Add(s);
                var dur = s.EndTime - s.StartTime;
                TotalListeningTime += dur;
                TotalHarmfulTime += s.HarmfulDuration;

                double startOff = Math.Clamp((s.StartTime - today).TotalSeconds, 0, daySecs);
                double endOff = Math.Clamp((s.EndTime - today).TotalSeconds, 0, daySecs);

                Bars.Add(new TimelineBar
                {
                    StartFrac = startOff / daySecs,
                    EndFrac = endOff / daySecs,
                    HarmfulFrac = dur.TotalSeconds > 0
                        ? Math.Min(1.0, s.HarmfulDuration.TotalSeconds / dur.TotalSeconds)
                        : 0.0,
                    DeviceName = s.DeviceName,
                    SessionLabel = $"{s.StartTime:HH:mm}–{s.EndTime:HH:mm}"
                });
            }

            // live session
            var live = _exposureMgr.CurrentSession;
            if (live != null)
            {
                var now = DateTime.Now;
                var dur = now - live.StartTime;
                TotalListeningTime += dur;
                TotalHarmfulTime += live.HarmfulDuration;

                double startOff = Math.Clamp((live.StartTime - today).TotalSeconds, 0, daySecs);
                double endOff = Math.Clamp((now - today).TotalSeconds, 0, daySecs);

                Bars.Add(new TimelineBar
                {
                    StartFrac = startOff / daySecs,
                    EndFrac = endOff / daySecs,
                    HarmfulFrac = live.WasHarmful
                        ? Math.Min(1.0, live.HarmfulDuration.TotalSeconds / dur.TotalSeconds)
                        : 0.0,
                    DeviceName = live.DeviceName,
                    SessionLabel = $"{live.StartTime:HH:mm}–{now:HH:mm}"
                });
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
