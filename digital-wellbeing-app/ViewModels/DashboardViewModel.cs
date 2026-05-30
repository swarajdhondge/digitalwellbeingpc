using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SettingsService _settingsService = new();
        private readonly ReportService _reportService = new();
        private readonly CoreLogic.ScreenTimeTracker? _screenTracker;
        private readonly DispatcherTimer _refreshTimer;
        
        private string _screenTime = string.Empty;
        private string _soundTime = string.Empty;
        private string _soundHarmfulTime = string.Empty;
        private string _thresholdLabel = string.Empty;
        private string _appTime = string.Empty;
        private ImageSource _topAppIcon = null!;
        private string _topAppName = string.Empty;
        private string _topAppDuration = string.Empty;

        // Sound status
        private string _soundStatus = "Normal";
        private bool _isSoundWarning = false;

        // Weekly summary
        private string _weeklyScreenTime = string.Empty;
        private string _weeklyChangeText = string.Empty;
        private bool _weeklyImproved = false;

        // Top apps for stacked bar
        private ObservableCollection<TopAppInfo> _topApps = new();
        
        /// <summary>Model for top apps display</summary>
        public class TopAppInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Duration { get; set; } = string.Empty;
            public TimeSpan TotalTime { get; set; }
            public double Percentage { get; set; }
            public ImageSource? Icon { get; set; }
            public string ExecutablePath { get; set; } = string.Empty;
            public int ColorIndex { get; set; }
        }

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

        public string SoundStatus
        {
            get => _soundStatus;
            set { if (_soundStatus != value) { _soundStatus = value; OnPropertyChanged(); } }
        }

        public bool IsSoundWarning
        {
            get => _isSoundWarning;
            set { if (_isSoundWarning != value) { _isSoundWarning = value; OnPropertyChanged(); } }
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

        /// <summary>Weekly total screen time formatted</summary>
        public string WeeklyScreenTime
        {
            get => _weeklyScreenTime;
            set { if (_weeklyScreenTime != value) { _weeklyScreenTime = value; OnPropertyChanged(); } }
        }

        /// <summary>Weekly change text (e.g., "↓ 12% vs last week")</summary>
        public string WeeklyChangeText
        {
            get => _weeklyChangeText;
            set { if (_weeklyChangeText != value) { _weeklyChangeText = value; OnPropertyChanged(); } }
        }

        /// <summary>Whether weekly screen time improved (decreased)</summary>
        public bool WeeklyImproved
        {
            get => _weeklyImproved;
            set { if (_weeklyImproved != value) { _weeklyImproved = value; OnPropertyChanged(); } }
        }

        /// <summary>Top 3 apps for stacked bar display</summary>
        public ObservableCollection<TopAppInfo> TopApps
        {
            get => _topApps;
            set { if (_topApps != value) { _topApps = value; OnPropertyChanged(); } }
        }

        public DashboardViewModel()
        {
            _screenTracker = (System.Windows.Application.Current as App)?.ScreenTracker;

            // Set up timer (don't start yet - wait for StartRefreshing)
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _refreshTimer.Tick += (_, __) => RefreshData();

            // Initial data load
            RefreshData();
        }

        /// <summary>
        /// Start periodic refresh. Call from view's Loaded/IsVisibleChanged event.
        /// Idempotent - safe to call multiple times.
        /// </summary>
        public void StartRefreshing()
        {
            if (_refreshTimer.IsEnabled) return;
            RefreshData();
            _refreshTimer.Start();
        }

        /// <summary>
        /// Stop periodic refresh. Call from view's Unloaded/IsVisibleChanged event.
        /// Idempotent - safe to call multiple times.
        /// </summary>
        public void StopRefreshing()
        {
            if (!_refreshTimer.IsEnabled) return;
            _refreshTimer.Stop();
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

            // — Screen Time (live from tracker, falls back to DB) —
            var tsScreen = _screenTracker?.CurrentActiveTime
                ?? TimeSpan.FromSeconds(
                    DatabaseService.GetScreenTimePeriodForToday()?.AccumulatedActiveSeconds ?? 0);
            ScreenTime = TimeFormatHelper.FormatDuration(tsScreen);

            // — Sound Sessions —
            var soundSessions = DatabaseService.GetSoundSessionsForDate(today);

            // total listening (use ActualListeningDuration, not wall-clock time)
            var tsSound = soundSessions.Aggregate(
                TimeSpan.Zero,
                (sum, s) => sum + s.ActualListeningDuration);
            SoundTime = TimeFormatHelper.FormatDuration(tsSound);

            // total harmful
            var tsHarm = soundSessions.Aggregate(
                TimeSpan.Zero,
                (sum, s) => sum + s.HarmfulDuration);
            SoundHarmfulTime = TimeFormatHelper.FormatDuration(tsHarm);

            // — Sound Status Badge —
            IsSoundWarning = tsHarm.TotalSeconds > 0;
            SoundStatus = IsSoundWarning ? "Loud" : "Normal";

            // — App Usage & Top Apps —
            var appSessions = DatabaseService.GetAppUsageSessionsForDate(today);
            var tsApp = appSessions.Aggregate(
                TimeSpan.Zero,
                (sum, s) => sum + (s.EndTime - s.StartTime));
            AppTime = TimeFormatHelper.FormatDuration(tsApp);

            if (appSessions.Count > 0)
            {
                var grouped = appSessions
                    .GroupBy(s => s.ExecutablePath)
                    .Select(g => new {
                        Path = g.Key,
                        Name = g.First().AppName,
                        Total = new TimeSpan(g.Sum(s => (s.EndTime - s.StartTime).Ticks))
                    })
                    .OrderByDescending(x => x.Total)
                    .Take(3)
                    .ToList();

                var top = grouped.First();
                TopAppName = AppNameService.GetDisplayName(top.Name, top.Path);
                TopAppDuration = TimeFormatHelper.FormatDuration(top.Total);
                TopAppIcon = AppIconService.GetIconForExe(top.Path) ?? null!;

                // Build top apps list for stacked bar
                var totalTicks = grouped.Sum(x => x.Total.Ticks);
                var topAppsList = grouped.Select((app, index) => new TopAppInfo
                {
                    Name = AppNameService.GetDisplayName(app.Name, app.Path),
                    Duration = TimeFormatHelper.FormatDuration(app.Total),
                    TotalTime = app.Total,
                    Percentage = totalTicks > 0 ? (double)app.Total.Ticks / totalTicks * 100 : 0,
                    Icon = AppIconService.GetIconForExe(app.Path),
                    ExecutablePath = app.Path,
                    ColorIndex = index
                }).ToList();

                TopApps = new ObservableCollection<TopAppInfo>(topAppsList);
            }
            else
            {
                TopAppName = "—";
                TopAppDuration = "0 m";
                TopAppIcon = null!;
                TopApps = new ObservableCollection<TopAppInfo>();
            }

            // — Weekly Summary —
            var (weeklyTotal, changePercent, improved) = _reportService.GetCurrentWeekSummary();
            WeeklyScreenTime = TimeFormatHelper.FormatCompact(weeklyTotal);
            WeeklyImproved = improved;
            
            if (Math.Abs(changePercent) < 1)
            {
                WeeklyChangeText = "same as last week";
            }
            else
            {
                var arrow = improved ? "↓" : "↑";
                WeeklyChangeText = $"{arrow} {Math.Abs(changePercent):F0}% vs last week";
            }

            await Task.CompletedTask;
        }

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
