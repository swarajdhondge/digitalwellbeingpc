using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;
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

        // Daily goal / day ring
        private bool _hasGoal;
        private double _ringFraction;
        private string _goalText = "No goal set";
        private bool _isOverGoal;

        // This-week bar chart + categories + sparkline
        private ObservableCollection<WeekBar> _weekBars = new();
        private ObservableCollection<CategoryRow> _categories = new();
        private string _weekTotalText = string.Empty;
        private string _weekAvgText = string.Empty;
        private string _vsYesterdayText = string.Empty;
        private bool _hasVsYesterday;
        private PointCollection _sparkLine = new();
        private PointCollection _sparkArea = new();

        // Top apps for stacked bar
        private ObservableCollection<TopAppInfo> _topApps = new();
        
        /// <summary>A single weekday column in the "This week" bar chart.</summary>
        public class WeekBar
        {
            public string Label { get; set; } = string.Empty;
            public double HeightPx { get; set; }      // against a fixed 110px chart
            public double Opacity { get; set; } = 0.32;
            public bool IsToday { get; set; }
        }

        /// <summary>A category row (tile + bar + duration) on the dashboard.</summary>
        public class CategoryRow
        {
            public string Name { get; set; } = string.Empty;
            public string Icon { get; set; } = "ShapeOutline";
            public string Duration { get; set; } = string.Empty;
            public double BarFraction { get; set; }   // 0..1 of the busiest category
            public Brush Tint { get; set; } = Brushes.Gray;
        }

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

        /// <summary>Whether a daily screen-time goal is set.</summary>
        public bool HasGoal
        {
            get => _hasGoal;
            set { if (_hasGoal != value) { _hasGoal = value; OnPropertyChanged(); } }
        }

        /// <summary>Day-ring fill fraction (0..1), clamped at the goal.</summary>
        public double RingFraction
        {
            get => _ringFraction;
            set { if (Math.Abs(_ringFraction - value) > 0.0001) { _ringFraction = value; OnPropertyChanged(); } }
        }

        /// <summary>Goal caption, e.g. "of 6h goal · 87%" or "No goal set".</summary>
        public string GoalText
        {
            get => _goalText;
            set { if (_goalText != value) { _goalText = value; OnPropertyChanged(); } }
        }

        /// <summary>True when today's screen time has passed the goal.</summary>
        public bool IsOverGoal
        {
            get => _isOverGoal;
            set { if (_isOverGoal != value) { _isOverGoal = value; OnPropertyChanged(); } }
        }

        /// <summary>"This week" 7-day bar chart.</summary>
        public ObservableCollection<WeekBar> WeekBars
        {
            get => _weekBars;
            set { _weekBars = value; OnPropertyChanged(); }
        }
        public string WeekTotalText { get => _weekTotalText; set { if (_weekTotalText != value) { _weekTotalText = value; OnPropertyChanged(); } } }
        public string WeekAvgText { get => _weekAvgText; set { if (_weekAvgText != value) { _weekAvgText = value; OnPropertyChanged(); } } }
        public string VsYesterdayText { get => _vsYesterdayText; set { if (_vsYesterdayText != value) { _vsYesterdayText = value; OnPropertyChanged(); } } }
        public bool HasVsYesterday { get => _hasVsYesterday; set { if (_hasVsYesterday != value) { _hasVsYesterday = value; OnPropertyChanged(); } } }

        /// <summary>Category breakdown (tile + bar + duration).</summary>
        public ObservableCollection<CategoryRow> Categories
        {
            get => _categories;
            set { _categories = value; OnPropertyChanged(); }
        }

        /// <summary>Sparkline geometry for the "Most used" card (280x46 space).</summary>
        public PointCollection SparkLine { get => _sparkLine; set { _sparkLine = value; OnPropertyChanged(); } }
        public PointCollection SparkArea { get => _sparkArea; set { _sparkArea = value; OnPropertyChanged(); } }

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

            // — Daily goal / day ring —
            var goalService = new GoalService();
            var goalMinutes = goalService.GetDailyScreenTimeGoal();
            HasGoal = goalMinutes != null;
            if (HasGoal)
            {
                var progress = goalService.GetGoalProgress(tsScreen);
                RingFraction = Math.Clamp(progress, 0, 1);
                IsOverGoal = progress > 1.0;
                var goalTs = TimeSpan.FromMinutes(goalMinutes!.Value);
                GoalText = $"of {TimeFormatHelper.FormatCompact(goalTs)} goal · {(int)(progress * 100)}%";
            }
            else
            {
                RingFraction = 0;
                IsOverGoal = false;
                GoalText = "No goal set";
            }

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

            // — This week bar chart + sparkline (real daily trend) —
            var weekStart = ReportService.GetWeekStart(today);
            var weekEnd = ReportService.GetWeekEnd(today);
            var trend = _reportService.GetDailyScreenTimeTrend(weekStart, weekEnd);
            var dayLabels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            double maxMin = Math.Max(1, trend.Count > 0 ? trend.Max(d => d.TotalSeconds / 60.0) : 1);
            int todayIdx = trend.FindIndex(d => d.Date.Date == today);

            var bars = new ObservableCollection<WeekBar>();
            for (int i = 0; i < trend.Count && i < 7; i++)
            {
                double min = trend[i].TotalSeconds / 60.0;
                bars.Add(new WeekBar
                {
                    Label = i < dayLabels.Length ? dayLabels[i] : trend[i].Date.ToString("ddd"),
                    HeightPx = Math.Max(6, (min / maxMin) * 110),
                    IsToday = i == todayIdx,
                    Opacity = i == todayIdx ? 1.0 : 0.32,
                });
            }
            WeekBars = bars;

            var weekTotalMin = trend.Sum(d => d.TotalSeconds / 60.0);
            WeekTotalText = TimeFormatHelper.FormatCompact(TimeSpan.FromMinutes(weekTotalMin));
            WeekAvgText = TimeFormatHelper.FormatCompact(TimeSpan.FromMinutes(weekTotalMin / 7.0));

            if (todayIdx > 0)
            {
                double diff = (trend[todayIdx].TotalSeconds - trend[todayIdx - 1].TotalSeconds) / 60.0;
                HasVsYesterday = true;
                var sign = diff >= 0 ? "+" : "−";
                VsYesterdayText = $"{sign}{TimeFormatHelper.FormatCompact(TimeSpan.FromMinutes(Math.Abs(diff)))} vs yesterday";
            }
            else { HasVsYesterday = false; }

            BuildSpark(trend.Select(d => d.TotalSeconds / 60.0).ToList());

            // — Categories today (grouped by app category) —
            var appsForCats = _reportService.GetTopAppsForPeriod(today, today, 40);
            var catGroups = appsForCats
                .GroupBy(a => a.Category)
                .Select(g => new { Cat = g.Key, Sec = g.Sum(a => a.TotalSeconds) })
                .Where(x => x.Sec > 0)
                .OrderByDescending(x => x.Sec)
                .ToList();
            double catMax = Math.Max(1, catGroups.Count > 0 ? catGroups.Max(x => x.Sec) : 1);
            var cats = new ObservableCollection<CategoryRow>();
            foreach (var g in catGroups)
            {
                cats.Add(new CategoryRow
                {
                    Name = CatName(g.Cat),
                    Icon = CatIcon(g.Cat),
                    Duration = TimeFormatHelper.FormatCompact(TimeSpan.FromSeconds(g.Sec)),
                    BarFraction = g.Sec / catMax,
                    Tint = BrushFromHex(CatHex(g.Cat)),
                });
            }
            Categories = cats;

            await Task.CompletedTask;
        }

        private void BuildSpark(System.Collections.Generic.List<double> data)
        {
            const double w = 280, h = 46;
            if (data.Count < 2) { SparkLine = new PointCollection(); SparkArea = new PointCollection(); return; }
            double max = data.Max(), min = data.Min();
            double range = (max - min) <= 0 ? 1 : (max - min);
            var line = new PointCollection();
            for (int i = 0; i < data.Count; i++)
            {
                double x = (i / (double)(data.Count - 1)) * w;
                double y = h - ((data[i] - min) / range) * (h - 8) - 4;
                line.Add(new Point(x, y));
            }
            var area = new PointCollection { new Point(0, h) };
            foreach (var p in line) area.Add(p);
            area.Add(new Point(w, h));
            SparkLine = line;
            SparkArea = area;
        }

        private static string CatName(AppCategoryType c) => c switch
        {
            AppCategoryType.Work => "Work",
            AppCategoryType.Entertainment => "Entertainment",
            _ => "Neutral",
        };
        private static string CatIcon(AppCategoryType c) => c switch
        {
            AppCategoryType.Work => "BriefcaseOutline",
            AppCategoryType.Entertainment => "PlayCircleOutline",
            _ => "ShapeOutline",
        };
        private static string CatHex(AppCategoryType c) => c switch
        {
            AppCategoryType.Work => "#5B86D6",
            AppCategoryType.Entertainment => "#C77F8E",
            _ => "#B79A6B",
        };
        private static Brush BrushFromHex(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
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
