using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using digital_wellbeing_app.CoreLogic;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.ViewModels
{
    public class AppUsageViewModel : INotifyPropertyChanged, IDisposable
    {
        public ObservableCollection<AppUsageSummary> TodaysUsage { get; } = [];

        private readonly AppUsageTracker _tracker;
        private readonly DispatcherTimer _timer;
        private bool _disposed;
        private int _listRefreshTicks;

        // Day-view state
        private bool _isDayView;
        private DateTime _selectedDate = DateTime.Today;
        private string _dailyScreenTime = string.Empty;
        private DateTime? _earliestDate;
        private const int ListRefreshIntervalSeconds = 5;

        #region Properties - Current App (Live)

        private string _currentAppName = string.Empty;
        public string CurrentAppName
        {
            get => _currentAppName;
            set { if (_currentAppName == value) return; _currentAppName = value; OnPropertyChanged(nameof(CurrentAppName)); }
        }

        private string _currentWindowTitle = string.Empty;
        public string CurrentWindowTitle
        {
            get => _currentWindowTitle;
            set { if (_currentWindowTitle == value) return; _currentWindowTitle = value; OnPropertyChanged(nameof(CurrentWindowTitle)); }
        }

        private string _currentAppDuration = "0m 0s";
        public string CurrentAppDuration
        {
            get => _currentAppDuration;
            set { if (_currentAppDuration == value) return; _currentAppDuration = value; OnPropertyChanged(nameof(CurrentAppDuration)); }
        }

        private BitmapImage? _currentAppIcon;
        public BitmapImage? CurrentAppIcon
        {
            get => _currentAppIcon;
            set { if (_currentAppIcon == value) return; _currentAppIcon = value; OnPropertyChanged(nameof(CurrentAppIcon)); }
        }

        private bool _hasCurrentApp;
        public bool HasCurrentApp
        {
            get => _hasCurrentApp;
            set { if (_hasCurrentApp == value) return; _hasCurrentApp = value; OnPropertyChanged(nameof(HasCurrentApp)); }
        }

        private string _currentAppExecutablePath = string.Empty;
        public string CurrentAppExecutablePath
        {
            get => _currentAppExecutablePath;
            set { if (_currentAppExecutablePath == value) return; _currentAppExecutablePath = value; OnPropertyChanged(nameof(CurrentAppExecutablePath)); }
        }

        #endregion

        #region Properties - Focus Stats

        private int _switchCount;
        public int SwitchCount
        {
            get => _switchCount;
            set { if (_switchCount == value) return; _switchCount = value; OnPropertyChanged(nameof(SwitchCount)); }
        }

        private string _averageFocusTime = "0m";
        public string AverageFocusTime
        {
            get => _averageFocusTime;
            set { if (_averageFocusTime == value) return; _averageFocusTime = value; OnPropertyChanged(nameof(AverageFocusTime)); }
        }

        private string _longestSessionTime = "0m";
        public string LongestSessionTime
        {
            get => _longestSessionTime;
            set { if (_longestSessionTime == value) return; _longestSessionTime = value; OnPropertyChanged(nameof(LongestSessionTime)); }
        }

        #endregion

        #region Properties - State

        private bool _isTracking = true;
        public bool IsTracking
        {
            get => _isTracking;
            set { if (_isTracking == value) return; _isTracking = value; OnPropertyChanged(nameof(IsTracking)); }
        }

        private bool _hasApps;
        public bool HasApps
        {
            get => _hasApps;
            set { if (_hasApps == value) return; _hasApps = value; OnPropertyChanged(nameof(HasApps)); }
        }

        private DateTime _selectedWeekStart = WeekNavigationHelper.StartOfWeek(DateTime.Today);
        public DateTime SelectedWeekStart
        {
            get => _selectedWeekStart;
            private set { if (_selectedWeekStart == value) return; _selectedWeekStart = value; OnPropertyChanged(nameof(SelectedWeekStart)); }
        }

        private DateTime? _earliestWeekStart;
        public DateTime? EarliestWeekStart
        {
            get => _earliestWeekStart;
            private set { if (_earliestWeekStart == value) return; _earliestWeekStart = value; OnPropertyChanged(nameof(EarliestWeekStart)); }
        }

        public string WeekLabel => WeekNavigationHelper.FormatWeek(SelectedWeekStart);
        public bool CanGoPrevious => EarliestWeekStart.HasValue && SelectedWeekStart > EarliestWeekStart.Value;
        public bool CanGoNext => SelectedWeekStart < WeekNavigationHelper.StartOfWeek(DateTime.Today);
        public string WeekTotalText => TimeFormatHelper.FormatCompact(TimeSpan.FromSeconds(TodaysUsage.Sum(x => x.TotalDuration.TotalSeconds)));

        // --- Day-view properties ---

        public bool IsDayView
        {
            get => _isDayView;
            private set
            {
                if (_isDayView == value) return;
                _isDayView = value;
                OnPropertyChanged(nameof(IsDayView));
                OnPropertyChanged(nameof(RangeHeader));
                OnPropertyChanged(nameof(IsShowingToday));
            }
        }

        public DateTime SelectedDate
        {
            get => _selectedDate;
            private set
            {
                if (_selectedDate == value) return;
                _selectedDate = value;
                OnPropertyChanged(nameof(SelectedDate));
                OnPropertyChanged(nameof(DailyDateLabel));
                OnPropertyChanged(nameof(CanGoToPreviousDay));
                OnPropertyChanged(nameof(CanGoToNextDay));
                OnPropertyChanged(nameof(IsShowingToday));
            }
        }

        public string DailyScreenTime
        {
            get => _dailyScreenTime;
            private set { if (_dailyScreenTime == value) return; _dailyScreenTime = value; OnPropertyChanged(nameof(DailyScreenTime)); }
        }

        public string DailyDateLabel
        {
            get
            {
                var daysDiff = (DateTime.Today - _selectedDate.Date).Days;
                var rawDate = _selectedDate.ToString("MMMM d, yyyy");
                
                return daysDiff switch
                {
                    0 => $"Today • {rawDate}",
                    1 => $"Yesterday • {rawDate}",
                    _ when daysDiff <= 7 => $"{daysDiff} days ago • {rawDate}",
                    _ => rawDate
                };
            }
        }
        // Allow browsing back up to 365 days regardless of whether data exists for every day.
        public bool CanGoToPreviousDay => _selectedDate.Date > DateTime.Today.AddDays(-365);
        public bool CanGoToNextDay => _selectedDate.Date < DateTime.Today;
        public bool IsShowingToday => !_isDayView || _selectedDate.Date == DateTime.Today;

        #endregion

        public AppUsageViewModel()
        {
            _tracker = (System.Windows.Application.Current as App)?.AppTracker
                       ?? new AppUsageTracker();

            // Subscribe to app switch events
            _tracker.OnAppSwitched += OnAppSwitched;

            // The small "now using" clock updates every second. Database-backed rows and
            // aggregate metrics are refreshed separately at a much lower cadence below.
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateAll();

            var earliest = DatabaseService.GetEarliestAppUsageDate();
            EarliestWeekStart = earliest.HasValue ? WeekNavigationHelper.StartOfWeek(earliest.Value) : null;
            _earliestDate = earliest;

            // Initial load
            LoadTodaysUsage();
            UpdateCurrentApp();
            UpdateFocusStats();
        }

        /// <summary>Start live refresh — call from the view's Loaded/IsVisibleChanged. Idempotent.</summary>
        public void StartRefreshing()
        {
            if (_disposed || _timer.IsEnabled) return;
            LoadList();
            UpdateCurrentApp();
            UpdateFocusStats();
            _listRefreshTicks = 0;
            _timer.Start();
        }

        /// <summary>Stop live refresh — call from the view's Unloaded/IsVisibleChanged. Idempotent.</summary>
        public void StopRefreshing()
        {
            _timer.Stop();
        }

        private void OnAppSwitched()
        {
            // When app switches, reload the usage list
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher?.CheckAccess() == true)
            {
                LoadList();
                UpdateCurrentApp();
                UpdateFocusStats();
            }
            else
            {
                dispatcher?.Invoke(() =>
                {
                    LoadList();
                    UpdateCurrentApp();
                    UpdateFocusStats();
                });
            }
        }

        private void UpdateAll()
        {
            if (_disposed) return;

            UpdateCurrentApp();

            // Keep the per-app rows and their total current without querying SQLite every
            // second. The live card already ticks each second; five seconds is sufficient for
            // list accuracy while keeping navigation and idle CPU usage light.
            if (++_listRefreshTicks >= ListRefreshIntervalSeconds)
            {
                _listRefreshTicks = 0;
                LoadList();
                UpdateFocusStats();
            }
        }

        private void UpdateCurrentApp()
        {
            var session = _tracker.CurrentSession;
            HasCurrentApp = session != null;

            if (session != null)
            {
                CurrentAppName = AppNameService.GetDisplayName(session.AppName, session.ExecutablePath);
                CurrentWindowTitle = TruncateWindowTitle(session.WindowTitle ?? string.Empty);

                // Calculate live duration
                var duration = DateTime.Now - session.StartTime;
                CurrentAppDuration = TimeFormatHelper.FormatCompact(duration);

                // Load icon if changed
                if (CurrentAppIcon == null || !string.Equals(_currentIconPath, session.ExecutablePath))
                {
                    _currentIconPath = session.ExecutablePath;
                    CurrentAppExecutablePath = session.ExecutablePath;
                    CurrentAppIcon = AppIconService.GetIconForExe(session.ExecutablePath);
                }

                IsTracking = true;
            }
            else
            {
                CurrentAppName = "No app active";
                CurrentWindowTitle = string.Empty;
                CurrentAppDuration = "—";
                CurrentAppExecutablePath = string.Empty;
                IsTracking = false;
            }
        }

        private string? _currentIconPath;

        private void UpdateFocusStats()
        {
            var sessions = _isWeekView
                ? GetSelectedWeekSessions(includeLive: false)
                : _isDayView
                    ? DatabaseService.GetAppUsageSessionsForDate(_selectedDate)
                    : DatabaseService.GetAppUsageSessionsForDate(DateTime.Now);

            // Fold in the live session only when the selected range contains today.
            var currentSession =
                (!_isWeekView && !_isDayView) ||
                (_isWeekView && SelectedWeekStart == WeekNavigationHelper.StartOfWeek(DateTime.Today)) ||
                (_isDayView && _selectedDate.Date == DateTime.Today)
                    ? _tracker.CurrentSession
                    : null;
            var allSessions = sessions.ToList();
            var metrics = AppUsageMetrics.Calculate(allSessions, currentSession, DateTime.Now);

            if (allSessions.Count == 0 && currentSession == null)
            {
                SwitchCount = 0;
                AverageFocusTime = "0m";
                LongestSessionTime = "0m";
                return;
            }

            SwitchCount = metrics.SwitchCount;
            LongestSessionTime = TimeFormatHelper.FormatFocusMetric(metrics.LongestFocusTime);
            AverageFocusTime = TimeFormatHelper.FormatFocusMetric(metrics.AverageFocusTime);
        }

        private bool _isWeekView;
        /// <summary>True when the Today/Week toggle is on "Week".</summary>
        public bool IsWeekView
        {
            get => _isWeekView;
            private set
            {
                if (_isWeekView == value) return;
                _isWeekView = value;
                OnPropertyChanged(nameof(IsWeekView));
                OnPropertyChanged(nameof(RangeHeader));
            }
        }

        /// <summary>Header for the app list, reflecting the selected range.</summary>
        public string RangeHeader => _isDayView ? "APP USAGE" : _isWeekView ? "APPS THIS WEEK" : "TODAY'S APPS";

        /// <summary>Called by the view when the Today/Week segmented toggle changes.</summary>
        public void SetWeekView(bool week)
        {
            // Leaving day-view when switching to Today or Week
            if (_isDayView) IsDayView = false;
            if (_isWeekView == week) return;
            IsWeekView = week;
            LoadList();
            UpdateFocusStats();
        }

        /// <summary>Switch to the History / day-view mode (called by the "History" radio button).</summary>
        public void SetDayView()
        {
            if (_isDayView) return;
            if (_isWeekView) IsWeekView = false;
            IsDayView = true;
            // Always start day-view on today so the user sees fresh data immediately.
            SelectedDate = DateTime.Today;
            LoadDayUsage();
            UpdateFocusStats();
        }

        public void GoToPreviousDay()
        {
            if (!CanGoToPreviousDay) return;
            SelectedDate = _selectedDate.AddDays(-1);
            LoadDayUsage();
            UpdateFocusStats();
        }

        public void GoToNextDay()
        {
            if (!CanGoToNextDay) return;
            SelectedDate = _selectedDate.AddDays(1);
            LoadDayUsage();
            UpdateFocusStats();
        }

        public void GoToPreviousWeek()
        {
            if (CanGoPrevious) GoToWeek(SelectedWeekStart.AddDays(-7));
        }

        public void GoToNextWeek()
        {
            if (CanGoNext) GoToWeek(SelectedWeekStart.AddDays(7));
        }

        public void GoToWeek(DateTime date)
        {
            SelectedWeekStart = WeekNavigationHelper.Clamp(date, EarliestWeekStart);
            OnPropertyChanged(nameof(WeekLabel));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            LoadWeekUsage();
            UpdateFocusStats();
        }

        /// <summary>
        /// Navigate to History mode for a specific calendar date.
        /// Called from outside (e.g. Dashboard week-bar clicks, Screen Time day-row clicks)
        /// to deep-link into the History tab at the requested date.
        /// The date is clamped so it can never exceed today or go beyond the 365-day boundary.
        /// </summary>
        public void GoToHistoryDate(DateTime date)
        {
            var clamped = date.Date <= DateTime.Today ? date.Date : DateTime.Today;
            if (_isWeekView) IsWeekView = false;
            IsDayView = true;
            SelectedDate = clamped;
            LoadDayUsage();
            UpdateFocusStats();
        }

        private void LoadList()
        {
            if (_isDayView) LoadDayUsage();
            else if (_isWeekView) LoadWeekUsage();
            else LoadTodaysUsage();
        }

        private void LoadWeekUsage()
        {
            // Aggregate the selected Monday-to-Sunday week per app.
            var sessions = GetSelectedWeekSessions(includeLive: true);
            var grouped = sessions
                .GroupBy(s => AppIdentity.NormalizeKey(s.ExecutablePath, s.AppName))
                .Where(g => g.Key.Length > 0)
                .Select(g => new AppUsageSummary
                {
                    AppName = AppNameService.GetDisplayName(g.First().AppName, g.First().ExecutablePath),
                    ExecutablePath = g.First().ExecutablePath,
                    TotalDuration = TimeSpan.FromSeconds(g.Sum(s => s.Duration.TotalSeconds))
                })
                .OrderByDescending(x => x.TotalDuration)
                .ToList();

            TodaysUsage.Clear();
            foreach (var item in grouped)
                TodaysUsage.Add(item);
            HasApps = TodaysUsage.Count > 0;
            OnPropertyChanged(nameof(WeekTotalText));
        }

        private void LoadTodaysUsage()
        {
            // Shared "today so far" source (persisted + live session) so this page's total
            // matches the Dashboard's App Time exactly.
            var grouped = LiveUsageProvider.GetTodayAppEntries()
                .Select(e => new AppUsageSummary
                {
                    AppName = AppNameService.GetDisplayName(e.AppName, e.ExecutablePath),
                    ExecutablePath = e.ExecutablePath,
                    TotalDuration = e.Duration
                })
                .ToList();

            TodaysUsage.Clear();
            foreach (var item in grouped)
            {
                TodaysUsage.Add(item);
            }

            HasApps = TodaysUsage.Count > 0;
            OnPropertyChanged(nameof(WeekTotalText));
        }

        /// <summary>
        /// Load usage data for <see cref="SelectedDate"/>. Uses <see cref="LiveUsageProvider"/>
        /// for today (includes the live un-flushed session) and raw DB sessions for past days.
        /// </summary>
        private void LoadDayUsage()
        {
            // --- Per-app breakdown for the selected day ---
            List<AppUsageSummary> appList;
            if (_selectedDate.Date == DateTime.Today)
            {
                // Reuse today's live+persisted source so the total matches the Dashboard.
                appList = LiveUsageProvider.GetTodayAppEntries()
                    .Select(e => new AppUsageSummary
                    {
                        AppName = AppNameService.GetDisplayName(e.AppName, e.ExecutablePath),
                        ExecutablePath = e.ExecutablePath,
                        TotalDuration = e.Duration
                    })
                    .ToList();
            }
            else
            {
                // Historical day: group the raw DB sessions for that calendar date.
                appList = DatabaseService.GetAppUsageSessionsForDate(_selectedDate)
                    .GroupBy(s => AppIdentity.NormalizeKey(s.ExecutablePath, s.AppName))
                    .Where(g => g.Key.Length > 0)
                    .Select(g => new AppUsageSummary
                    {
                        AppName = AppNameService.GetDisplayName(g.First().AppName, g.First().ExecutablePath),
                        ExecutablePath = g.First().ExecutablePath,
                        TotalDuration = TimeSpan.FromSeconds(g.Sum(s => s.Duration.TotalSeconds))
                    })
                    .OrderByDescending(x => x.TotalDuration)
                    .ToList();
            }

            // --- Screen-time total: sum of all app durations (single source of truth) ---
            // Previously this read from the independently-tracked ScreenTimePeriod table,
            // which counts passive-consumption time (video watching, gaming) that the
            // AppUsageTracker does not record. Deriving from the same AppUsageSession data
            // guarantees the headline matches the per-app breakdown.
            var totalDuration = appList.Aggregate(TimeSpan.Zero, (sum, a) => sum + a.TotalDuration);
            DailyScreenTime = TimeFormatHelper.FormatDuration(totalDuration);

            TodaysUsage.Clear();
            foreach (var item in appList)
                TodaysUsage.Add(item);
            HasApps = TodaysUsage.Count > 0;
            OnPropertyChanged(nameof(WeekTotalText));
        }

        private List<AppUsageSession> GetSelectedWeekSessions(bool includeLive)
        {
            var sessions = DatabaseService
                .GetAppUsageSessionsForRange(SelectedWeekStart, SelectedWeekStart.AddDays(6))
                .ToList();

            if (includeLive && SelectedWeekStart == WeekNavigationHelper.StartOfWeek(DateTime.Today))
            {
                var live = _tracker.CurrentSession;
                var now = DateTime.Now;
                if (live != null && now > live.StartTime)
                {
                    sessions.Add(new AppUsageSession
                    {
                        AppName = live.AppName,
                        ExecutablePath = live.ExecutablePath,
                        StartTime = live.StartTime,
                        EndTime = now
                    });
                }
            }

            return sessions;
        }

        private static string TruncateWindowTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return string.Empty;
            const int maxLen = 50;
            return title.Length <= maxLen ? title : title[..(maxLen - 3)] + "...";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _tracker.OnAppSwitched -= OnAppSwitched;
            GC.SuppressFinalize(this);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class AppUsageSummary : INotifyPropertyChanged
    {
        public string AppName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;

        private TimeSpan _totalDuration;
        public TimeSpan TotalDuration
        {
            get => _totalDuration;
            set
            {
                if (_totalDuration == value) return;
                _totalDuration = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalDuration)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DurationFormatted)));
            }
        }

        public string DurationFormatted => TimeFormatHelper.FormatCompact(TotalDuration);

        private BitmapImage? _icon;
        public BitmapImage? Icon
        {
            get
            {
                if (_icon == null && !string.IsNullOrEmpty(ExecutablePath))
                {
                    _icon = AppIconService.GetIconForExe(ExecutablePath);
                }
                return _icon;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
