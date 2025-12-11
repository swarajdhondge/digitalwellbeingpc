using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SettingsService _settingsService = new();
        private readonly DispatcherTimer _refreshTimer;
        
        private string _screenTime = string.Empty;
        private string _soundTime = string.Empty;
        private string _soundHarmfulTime = string.Empty;
        private string _thresholdLabel = string.Empty;
        private string _appTime = string.Empty;
        private ImageSource _topAppIcon = null!;
        private string _topAppName = string.Empty;
        private string _topAppDuration = string.Empty;

        public string ScreenTime
        {
            get => _screenTime;
            set { if (_screenTime != value) { _screenTime = value; OnPropertyChanged(); } }
        }

        /// <summary>Total listening time today</summary>
        public string SoundTime
        {
            get => _soundTime;
            set { if (_soundTime != value) { _soundTime = value; OnPropertyChanged(); } }
        }

        /// <summary>Total harmful exposure today</summary>
        public string SoundHarmfulTime
        {
            get => _soundHarmfulTime;
            set { if (_soundHarmfulTime != value) { _soundHarmfulTime = value; OnPropertyChanged(); } }
        }

        /// <summary>Dynamic threshold label based on settings</summary>
        public string ThresholdLabel
        {
            get => _thresholdLabel;
            set { if (_thresholdLabel != value) { _thresholdLabel = value; OnPropertyChanged(); } }
        }

        public string AppTime
        {
            get => _appTime;
            set { if (_appTime != value) { _appTime = value; OnPropertyChanged(); } }
        }

        public ImageSource TopAppIcon
        {
            get => _topAppIcon;
            set { if (_topAppIcon != value) { _topAppIcon = value; OnPropertyChanged(); } }
        }

        public string TopAppName
        {
            get => _topAppName;
            set { if (_topAppName != value) { _topAppName = value; OnPropertyChanged(); } }
        }

        public string TopAppDuration
        {
            get => _topAppDuration;
            set { if (_topAppDuration != value) { _topAppDuration = value; OnPropertyChanged(); } }
        }

        public DashboardViewModel()
        {
            // Initial load
            RefreshData();

            // Set up timer for periodic refresh (every 2 seconds)
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _refreshTimer.Tick += (_, __) => RefreshData();
            _refreshTimer.Start();
        }

        /// <summary>
        /// Public method to refresh all dashboard data including threshold from settings
        /// </summary>
        public void RefreshData()
        {
            _ = LoadTodayAsync();
        }

        private async Task LoadTodayAsync()
        {
            var today = DateTime.Today;

            // — Load threshold from settings —
            var threshold = _settingsService.LoadHarmfulThreshold();
            ThresholdLabel = $"ABOVE {(int)threshold} dB";

            // — Screen Time —
            var period = DatabaseService.GetScreenTimePeriodForToday();
            var tsScreen = period != null
                ? TimeSpan.FromSeconds(period.AccumulatedActiveSeconds)
                : TimeSpan.Zero;
            ScreenTime = Format(tsScreen);

            // — Sound Sessions —
            var soundSessions = DatabaseService.GetSoundSessionsForDate(today);

            // total listening (use ActualListeningDuration, not wall-clock time)
            var tsSound = soundSessions.Aggregate(
                TimeSpan.Zero,
                (sum, s) => sum + s.ActualListeningDuration);
            SoundTime = Format(tsSound);

            // total harmful
            var tsHarm = soundSessions.Aggregate(
                TimeSpan.Zero,
                (sum, s) => sum + s.HarmfulDuration);
            SoundHarmfulTime = Format(tsHarm);

            // — App Usage & Top App (unchanged) —
            var appSessions = DatabaseService.GetAppUsageSessionsForDate(today);
            var tsApp = appSessions.Aggregate(
                TimeSpan.Zero,
                (sum, s) => sum + (s.EndTime - s.StartTime));
            AppTime = Format(tsApp);

            if (appSessions.Count > 0)
            {
                var top = appSessions
                    .GroupBy(s => s.ExecutablePath)
                    .Select(g => new {
                        Path = g.Key,
                        Name = g.First().AppName,
                        Total = new TimeSpan(g.Sum(s => (s.EndTime - s.StartTime).Ticks))
                    })
                    .OrderByDescending(x => x.Total)
                    .First();

                TopAppName = top.Name;
                TopAppDuration = Format(top.Total);
                TopAppIcon = AppIconService.GetIconForExe(top.Path) ?? null!;
            }
            else
            {
                TopAppName = "—";
                TopAppDuration = "0:00:00";
                TopAppIcon = null!;
            }
            await Task.CompletedTask;
        }

        private static string Format(TimeSpan ts)
            => $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public void Dispose()
        {
            _refreshTimer.Stop();
            GC.SuppressFinalize(this);
        }
    }
}
