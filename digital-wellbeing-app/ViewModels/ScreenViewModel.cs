using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using digital_wellbeing_app.Helpers;
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

        private readonly ScreenTimeTracker _tracker;
        private readonly GoalService _goalService;
        private readonly DispatcherTimer _timer;
        private bool _disposed;

        // Throttle DB-heavy queries: update every 5 seconds instead of every 1 second
        private int _tickCounter;
        private const int DbRefreshInterval = 5;

        #region Properties - Today's Time

        private string _todayTimeText = "0 hr 0 min";
        public string TodayTimeText
        {
            get => _todayTimeText;
            set { if (_todayTimeText == value) return; _todayTimeText = value; OnPropertyChanged(nameof(TodayTimeText)); }
        }

        #endregion

        #region Properties - Tracking State (Phase 2.1)

        private TrackingState _trackingState = TrackingState.Active;
        public TrackingState TrackingState
        {
            get => _trackingState;
            set { if (_trackingState == value) return; _trackingState = value; OnPropertyChanged(nameof(TrackingState)); }
        }

        #endregion

        #region Properties - Context Line (Phase 2.2)

        private string _contextLine = string.Empty;
        public string ContextLine
        {
            get => _contextLine;
            set { if (_contextLine == value) return; _contextLine = value; OnPropertyChanged(nameof(ContextLine)); }
        }

        #endregion

        #region Properties - View Toggle

        private bool _isWeeklyView;
        public bool IsWeeklyView
        {
            get => _isWeeklyView;
            set 
            { 
                if (_isWeeklyView == value) return; 
                _isWeeklyView = value; 
                OnPropertyChanged(nameof(IsWeeklyView)); 
            }
        }

        #endregion

        #region Properties - Quick Stats (Phase 2.3)

        private int _sessionCount;
        public int SessionCount
        {
            get => _sessionCount;
            set { if (_sessionCount == value) return; _sessionCount = value; OnPropertyChanged(nameof(SessionCount)); }
        }

        private string _longestSession = "0 min";
        public string LongestSession
        {
            get => _longestSession;
            set { if (_longestSession == value) return; _longestSession = value; OnPropertyChanged(nameof(LongestSession)); }
        }

        private int _breakCount;
        public int BreakCount
        {
            get => _breakCount;
            set { if (_breakCount == value) return; _breakCount = value; OnPropertyChanged(nameof(BreakCount)); }
        }

        #endregion

        #region Properties - Goal System (Phase 3)

        private bool _hasGoal;
        public bool HasGoal
        {
            get => _hasGoal;
            set { if (_hasGoal == value) return; _hasGoal = value; OnPropertyChanged(nameof(HasGoal)); }
        }

        private double _goalProgress;
        public double GoalProgress
        {
            get => _goalProgress;
            set { if (Math.Abs(_goalProgress - value) < 0.001) return; _goalProgress = value; OnPropertyChanged(nameof(GoalProgress)); }
        }

        private string _goalProgressText = string.Empty;
        public string GoalProgressText
        {
            get => _goalProgressText;
            set { if (_goalProgressText == value) return; _goalProgressText = value; OnPropertyChanged(nameof(GoalProgressText)); }
        }

        private bool _isOverGoal;
        public bool IsOverGoal
        {
            get => _isOverGoal;
            set { if (_isOverGoal == value) return; _isOverGoal = value; OnPropertyChanged(nameof(IsOverGoal)); }
        }

        #endregion

        #region Properties - Weekly Stats (Phase 2.5)

        private string _weeklyAverageText = "0 hr 0 min";
        public string WeeklyAverageText
        {
            get => _weeklyAverageText;
            set { if (_weeklyAverageText == value) return; _weeklyAverageText = value; OnPropertyChanged(nameof(WeeklyAverageText)); }
        }

        private int _weeklyAverageMinutes;
        public int WeeklyAverageMinutes
        {
            get => _weeklyAverageMinutes;
            set { if (_weeklyAverageMinutes == value) return; _weeklyAverageMinutes = value; OnPropertyChanged(nameof(WeeklyAverageMinutes)); }
        }

        private string _weeklyTotalText = "0 m";
        public string WeeklyTotalText
        {
            get => _weeklyTotalText;
            set { if (_weeklyTotalText == value) return; _weeklyTotalText = value; OnPropertyChanged(nameof(WeeklyTotalText)); }
        }

        #endregion

        #region Properties - Week Navigation

        private DateTime _currentWeekStart;
        public DateTime CurrentWeekStart
        {
            get => _currentWeekStart;
            private set { if (_currentWeekStart == value) return; _currentWeekStart = value; OnPropertyChanged(nameof(CurrentWeekStart)); }
        }

        private string _weekLabel = string.Empty;
        public string WeekLabel
        {
            get => _weekLabel;
            set { if (_weekLabel == value) return; _weekLabel = value; OnPropertyChanged(nameof(WeekLabel)); }
        }

        private bool _canGoForward;
        public bool CanGoForward
        {
            get => _canGoForward;
            set { if (_canGoForward == value) return; _canGoForward = value; OnPropertyChanged(nameof(CanGoForward)); }
        }

        private bool _canGoBackward;
        public bool CanGoBackward
        {
            get => _canGoBackward;
            set { if (_canGoBackward == value) return; _canGoBackward = value; OnPropertyChanged(nameof(CanGoBackward)); }
        }

        // Monday of the earliest tracked week; null until computed (or if there's no data).
        private DateTime? _earliestWeekStart;
        public DateTime? EarliestWeekStart
        {
            get => _earliestWeekStart;
            private set { if (_earliestWeekStart == value) return; _earliestWeekStart = value; OnPropertyChanged(nameof(EarliestWeekStart)); }
        }

        private bool _isCurrentWeek = true;
        public bool IsCurrentWeek
        {
            get => _isCurrentWeek;
            set { if (_isCurrentWeek == value) return; _isCurrentWeek = value; OnPropertyChanged(nameof(IsCurrentWeek)); }
        }

        #endregion

        public ScreenViewModel()
        {
            _tracker = (System.Windows.Application.Current as App)?.ScreenTracker
                       ?? new ScreenTimeTracker();
            _goalService = new GoalService();

            // Initialize week navigation to current week's Monday
            CurrentWeekStart = WeekNavigationHelper.StartOfWeek(DateTime.Today);

            // Subscribe to state changes (named method for proper unsubscription)
            _tracker.StateChanged += OnTrackerStateChanged;
            TrackingState = _tracker.State;

            // Subscribe to goal changes from Settings
            GoalService.GoalChanged += OnGoalChanged;

            // Set up timer (don't start yet - wait for StartRefreshing)
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTimerTick;

            LoadWeeklyUsage();
            UpdateAll();
        }

        public void GoToPreviousWeek()
        {
            // Don't page back past the first week that has tracked data.
            if (!CanGoBackward) return;
            CurrentWeekStart = _currentWeekStart.AddDays(-7);
            UpdateWeekNavState();
            LoadWeeklyUsage();
        }

        public void GoToNextWeek()
        {
            var nextWeek = _currentWeekStart.AddDays(7);
            // Don't go past current week
            var currentMonday = DateTime.Today;
            while (currentMonday.DayOfWeek != DayOfWeek.Monday)
                currentMonday = currentMonday.AddDays(-1);
            if (nextWeek > currentMonday) return;

            CurrentWeekStart = nextWeek;
            UpdateWeekNavState();
            LoadWeeklyUsage();
        }

        public void GoToWeek(DateTime date)
        {
            CurrentWeekStart = WeekNavigationHelper.Clamp(date, EarliestWeekStart);
            UpdateWeekNavState();
            LoadWeeklyUsage();
        }

        private void UpdateWeekNavState()
        {
            var currentMonday = DateTime.Today;
            while (currentMonday.DayOfWeek != DayOfWeek.Monday)
                currentMonday = currentMonday.AddDays(-1);

            IsCurrentWeek = _currentWeekStart == currentMonday;
            CanGoForward = _currentWeekStart < currentMonday;

            // Resolve the earliest tracked week once, then disable "previous" at that floor.
            if (_earliestWeekStart == null)
            {
                var earliest = DatabaseService.GetEarliestScreenTimeDate();
                if (earliest != null)
                {
                    var m = earliest.Value.Date;
                    while (m.DayOfWeek != DayOfWeek.Monday) m = m.AddDays(-1);
                    EarliestWeekStart = m;
                }
            }
            CanGoBackward = _earliestWeekStart != null && _currentWeekStart > _earliestWeekStart.Value;

            WeekLabel = WeekNavigationHelper.FormatWeek(_currentWeekStart);
        }

        private void OnTrackerStateChanged(object? sender, TrackingState state)
        {
            TrackingState = state;
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            UpdateAll();
        }

        /// <summary>
        /// Start periodic refresh. Call from view's Loaded/IsVisibleChanged event.
        /// Idempotent - safe to call multiple times.
        /// </summary>
        public void StartRefreshing()
        {
            if (_timer.IsEnabled) return;
            _tickCounter = DbRefreshInterval; // Force DB queries on first call
            LoadWeeklyUsage();
            UpdateAll();
            _timer.Start();
        }

        /// <summary>
        /// Stop periodic refresh. Call from view's Unloaded/IsVisibleChanged event.
        /// Idempotent - safe to call multiple times.
        /// </summary>
        public void StopRefreshing()
        {
            if (!_timer.IsEnabled) return;
            _timer.Stop();
        }

        private void OnGoalChanged(object? sender, EventArgs e)
        {
            // Refresh goal when it's changed in Settings
            // Use dispatcher to ensure we're on the UI thread
            System.Diagnostics.Debug.WriteLine("[ScreenViewModel] OnGoalChanged event received");
            
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                System.Diagnostics.Debug.WriteLine("[ScreenViewModel] Dispatcher is null!");
                return;
            }
            
            if (dispatcher.CheckAccess())
            {
                // Already on UI thread
                System.Diagnostics.Debug.WriteLine("[ScreenViewModel] Already on UI thread, calling RefreshGoal directly");
                RefreshGoal();
            }
            else
            {
                // Need to invoke on UI thread
                System.Diagnostics.Debug.WriteLine("[ScreenViewModel] Invoking RefreshGoal on UI thread");
                dispatcher.Invoke(() => RefreshGoal());
            }
        }

        private void UpdateAll()
        {
            if (_disposed) return;

            // Lightweight updates every tick (1 second)
            UpdateTodayUsage();
            UpdateTrackingState();
            UpdateGoalProgress();
            UpdateTodayInWeeklyView();

            // DB-heavy updates only every N ticks
            _tickCounter++;
            if (_tickCounter >= DbRefreshInterval)
            {
                _tickCounter = 0;
                UpdateContextLine();
                UpdateQuickStats();
            }
        }

        private void UpdateTodayUsage()
        {
            // Use app-usage-based total (single source of truth) instead of the
            // ScreenTimeTracker counter which independently counts passive consumption.
            var ts = LiveUsageProvider.GetTodayActiveTime();
            TodayTimeText = TimeFormatHelper.FormatDuration(ts);

            UpdateTimelineSegments();
        }

        private void UpdateTrackingState()
        {
            TrackingState = _tracker.State;
            SessionCount = _tracker.SessionCount;
        }

        private void UpdateContextLine()
        {
            // Use app-usage sums for both today and yesterday so the comparison
            // is consistent with the per-app breakdown everywhere.
            var todayMinutes = (int)LiveUsageProvider.GetTodayActiveTime().TotalMinutes;

            var yesterday = DateTime.Today.AddDays(-1);
            var yesterdaySessions = DatabaseService.GetAppUsageSessionsForDate(yesterday);
            var yesterdayMinutes = (int)(yesterdaySessions.Sum(s => (s.EndTime - s.StartTime).TotalSeconds) / 60);

            if (yesterdayMinutes == 0)
            {
                ContextLine = "No data from yesterday";
                return;
            }

            var diff = todayMinutes - yesterdayMinutes;
            if (diff > 0)
            {
                ContextLine = $"↑ {TimeFormatHelper.FormatDuration(TimeSpan.FromMinutes(Math.Abs(diff)))} more than yesterday";
            }
            else if (diff < 0)
            {
                ContextLine = $"↓ {TimeFormatHelper.FormatDuration(TimeSpan.FromMinutes(Math.Abs(diff)))} less than yesterday";
            }
            else
            {
                ContextLine = "Same as yesterday";
            }
        }

        private void UpdateQuickStats()
        {
            var db = DatabaseService.GetConnection();
            var todayKey = DateTime.Today.ToString("yyyy-MM-dd");
            var sessions = db.Table<ScreenTimeSession>()
                            .Where(x => x.SessionDate == todayKey)
                            .OrderBy(x => x.StartTime)
                            .ToList();

            // Sessions count from tracker (includes current)
            SessionCount = Math.Max(1, _tracker.SessionCount);

            // Total active time for the day (this is the authoritative total)
            var totalActiveSec = (int)_tracker.CurrentActiveTime.TotalSeconds;

            // Calculate longest continuous session
            // Sessions saved every 5 min are merged if gap < 5 min (considered continuous)
            int longestSec = 0;
            
            if (sessions.Any())
            {
                // Merge adjacent sessions with small gaps into continuous sessions
                int currentMergedDuration = sessions[0].DurationSeconds;
                DateTime currentMergedEnd = sessions[0].StartTime.AddSeconds(sessions[0].DurationSeconds);
                
                for (int i = 1; i < sessions.Count; i++)
                {
                    var gap = (sessions[i].StartTime - currentMergedEnd).TotalMinutes;
                    
                    if (gap < 5) // Less than 5 min gap = same continuous session
                    {
                        // Extend merged session (only add active time, NOT gap time)
                        currentMergedDuration += sessions[i].DurationSeconds;
                        currentMergedEnd = sessions[i].StartTime.AddSeconds(sessions[i].DurationSeconds);
                    }
                    else
                    {
                        // Gap too large - this is a break. Check if previous was longest.
                        if (currentMergedDuration > longestSec)
                            longestSec = currentMergedDuration;
                        
                        // Start new merged session
                        currentMergedDuration = sessions[i].DurationSeconds;
                        currentMergedEnd = sessions[i].StartTime.AddSeconds(sessions[i].DurationSeconds);
                    }
                }
                
                // Check last merged session
                if (currentMergedDuration > longestSec)
                    longestSec = currentMergedDuration;
                
                // Add current continuous session's active time
                var continuousSessionSec = _tracker.ContinuousSessionSeconds;
                if (continuousSessionSec > 0 && _tracker.ContinuousSessionStart.HasValue)
                {
                    var gapFromLast = (_tracker.ContinuousSessionStart.Value - currentMergedEnd).TotalMinutes;
                    if (gapFromLast < 5 && gapFromLast >= 0)
                    {
                        // Current continuous session extends the last saved one
                        var totalCurrent = currentMergedDuration + continuousSessionSec;
                        if (totalCurrent > longestSec)
                            longestSec = totalCurrent;
                    }
                    else if (continuousSessionSec > longestSec)
                    {
                        // Current is a separate session
                        longestSec = continuousSessionSec;
                    }
                }
            }
            else
            {
                // No saved sessions yet - use current continuous session time
                longestSec = _tracker.ContinuousSessionSeconds;
            }

            // Sanity check: longest can never exceed total active time
            if (longestSec > totalActiveSec)
                longestSec = totalActiveSec;

            LongestSession = TimeFormatHelper.FormatDuration(TimeSpan.FromSeconds(longestSec));

            // Breaks (gaps >= 15 minutes between sessions)
            int breaks = 0;
            if (sessions.Count > 1)
            {
                DateTime lastEnd = sessions[0].StartTime.AddSeconds(sessions[0].DurationSeconds);
                
                for (int i = 1; i < sessions.Count; i++)
                {
                    var gap = (sessions[i].StartTime - lastEnd).TotalMinutes;
                    if (gap >= 15)
                        breaks++;
                    lastEnd = sessions[i].StartTime.AddSeconds(sessions[i].DurationSeconds);
                }
            }
            BreakCount = breaks;
        }

        private void UpdateGoalProgress()
        {
            var goal = _goalService.GetDailyScreenTimeGoal();
            System.Diagnostics.Debug.WriteLine($"[ScreenViewModel] UpdateGoalProgress: goal from service = {goal}");
            HasGoal = goal.HasValue;

            if (HasGoal)
            {
                var currentTime = LiveUsageProvider.GetTodayActiveTime();
                GoalProgress = _goalService.GetGoalProgress(currentTime);
                GoalProgressText = _goalService.FormatProgressText(currentTime);
                IsOverGoal = _goalService.IsOverGoal(currentTime);
                System.Diagnostics.Debug.WriteLine($"[ScreenViewModel] Goal enabled: {goal} min, progress={GoalProgress:P1}");
            }
            else
            {
                GoalProgress = 0;
                GoalProgressText = string.Empty;
                IsOverGoal = false;
                System.Diagnostics.Debug.WriteLine("[ScreenViewModel] No goal set");
            }
        }

        /// <summary>
        /// Update just today's entry in the weekly view (called every second).
        /// Only runs when viewing the current week.
        /// </summary>
        private void UpdateTodayInWeeklyView()
        {
            if (!IsCurrentWeek) return;

            var todayItem = WeeklyUsage.FirstOrDefault(x => x.IsToday);
            if (todayItem == null) return;

            var ts = LiveUsageProvider.GetTodayActiveTime();
            var newUsage = TimeFormatHelper.FormatDuration(ts);
            var newMinutes = (int)ts.TotalMinutes;

            if (todayItem.Usage != newUsage)
            {
                todayItem.Usage = newUsage;
                todayItem.Minutes = newMinutes;
                todayItem.Seconds = (int)ts.TotalSeconds;

                // Recalculate bar percentages (today's value may now be the new max)
                RecalculateBarPercentages();

                // Recalculate weekly average
                var totalSeconds = WeeklyUsage.Sum(x => x.Seconds);
                WeeklyTotalText = TimeFormatHelper.FormatDuration(TimeSpan.FromSeconds(totalSeconds));
                WeeklyAverageMinutes = (int)(totalSeconds / 60L / 7L);
                WeeklyAverageText = TimeFormatHelper.FormatDuration(TimeSpan.FromMinutes(WeeklyAverageMinutes));

                OnPropertyChanged(nameof(WeeklyUsage));
            }
        }

        public void LoadWeeklyUsage()
        {
            if (_disposed) return;
            WeeklyUsage.Clear();

            // Update week nav label
            UpdateWeekNavState();

            // Load all app-usage sessions for the displayed week once, then bucket by day.
            // This replaces the old ScreenTimePeriod lookup so the weekly bars are consistent
            // with the per-app breakdown everywhere.
            var weekEnd = _currentWeekStart.AddDays(6);
            var allSessions = DatabaseService.GetAppUsageSessionsForRange(_currentWeekStart, weekEnd);
            var buckets = allSessions
                .GroupBy(s => s.StartTime.Date)
                .ToDictionary(g => g.Key, g => (int)g.Sum(s => (s.EndTime - s.StartTime).TotalSeconds));

            long totalSeconds = 0;

            for (int i = 0; i < 7; i++)
            {
                var day = _currentWeekStart.AddDays(i);
                bool isToday = day.Date == DateTime.Today;

                int sec;
                if (isToday)
                {
                    sec = (int)LiveUsageProvider.GetTodayActiveTime().TotalSeconds;
                }
                else
                {
                    buckets.TryGetValue(day.Date, out sec);
                }

                var ts = TimeSpan.FromSeconds(sec);

                if (sec > 0)
                {
                    totalSeconds += sec;
                }

                WeeklyUsage.Add(new WeeklyUsageItem
                {
                    Day = day.DayOfWeek.ToString(),
                    Usage = TimeFormatHelper.FormatDuration(ts),
                    Minutes = (int)ts.TotalMinutes,
                    Seconds = sec,
                    IsToday = isToday,
                    Date = day.Date
                });
            }

            // Calculate proportional bar percentages (relative to max day)
            RecalculateBarPercentages();
            WeeklyTotalText = TimeFormatHelper.FormatDuration(TimeSpan.FromSeconds(totalSeconds));

            // Calculate weekly average
            if (totalSeconds > 0)
            {
                WeeklyAverageMinutes = (int)(totalSeconds / 60L / 7L);
                WeeklyAverageText = TimeFormatHelper.FormatDuration(TimeSpan.FromMinutes(WeeklyAverageMinutes));
            }
            else
            {
                WeeklyAverageMinutes = 0;
                WeeklyAverageText = "0 m";
            }
        }

        private void RecalculateBarPercentages()
        {
            var maxMinutes = WeeklyUsage.Max(x => x.Minutes);
            if (maxMinutes <= 0) maxMinutes = 1; // avoid division by zero
            foreach (var item in WeeklyUsage)
            {
                item.Percentage = (double)item.Minutes / maxMinutes * 100.0;
            }
        }

        private void UpdateTimelineSegments()
        {
            TimelineSegments.Clear();
            var db = DatabaseService.GetConnection();
            var todayKey = DateTime.Today.ToString("yyyy-MM-dd");

            // 1) Saved session segments
            var sessions = db.Table<ScreenTimeSession>()
                             .Where(x => x.SessionDate == todayKey)
                             .ToList();

            var dayStart = DateTime.Today;
            var dayEnd = dayStart.AddDays(1);
            var intervals = sessions
                .Where(s => s.DurationSeconds > 0)
                .Select(s => (Start: s.StartTime < dayStart ? dayStart : s.StartTime,
                              End: s.StartTime.AddSeconds(s.DurationSeconds) > dayEnd
                                  ? dayEnd
                                  : s.StartTime.AddSeconds(s.DurationSeconds)))
                .Where(i => i.End > i.Start)
                .ToList();

            // 2) Live session segment – use actual session start time
            var sessionStart = _tracker.CurrentSessionStart;
            var sessionSeconds = _tracker.CurrentSessionSeconds;
            if (sessionStart.HasValue && sessionSeconds > 0)
            {
                // Only show if session started today
                if (sessionStart.Value.Date == DateTime.Today)
                {
                    intervals.Add((sessionStart.Value,
                        sessionStart.Value.AddSeconds(sessionSeconds) > dayEnd
                            ? dayEnd
                            : sessionStart.Value.AddSeconds(sessionSeconds)));
                }
            }

            // Five-minute persistence chunks are implementation details, not separate visual
            // sessions. Merge touching/overlapping chunks so a continuous stretch renders as one
            // clean bar without seams, and clamp every bar inside the local-day bounds.
            var merged = new System.Collections.Generic.List<(DateTime Start, DateTime End)>();
            foreach (var interval in intervals.OrderBy(i => i.Start))
            {
                if (merged.Count > 0 && interval.Start <= merged[^1].End.AddSeconds(2))
                {
                    var previous = merged[^1];
                    merged[^1] = (previous.Start, interval.End > previous.End ? interval.End : previous.End);
                }
                else
                {
                    merged.Add(interval);
                }
            }

            var daySeconds = TimeSpan.FromDays(1).TotalSeconds;
            foreach (var interval in merged)
            {
                var startSeconds = (interval.Start - dayStart).TotalSeconds;
                var durationSeconds = (interval.End - interval.Start).TotalSeconds;
                TimelineSegments.Add(new ScreenTimelineSegment
                {
                    StartPercent = Math.Clamp(startSeconds / daySeconds, 0, 1),
                    WidthPercent = Math.Clamp(durationSeconds / daySeconds, 0, 1 - startSeconds / daySeconds)
                });
            }

            OnPropertyChanged(nameof(TimelineSegments));
        }

        /// <summary>
        /// Refresh goal from service (call after settings change)
        /// </summary>
        public void RefreshGoal()
        {
            System.Diagnostics.Debug.WriteLine("[ScreenViewModel] RefreshGoal called");
            _goalService.InvalidateCache();
            UpdateGoalProgress();
            System.Diagnostics.Debug.WriteLine($"[ScreenViewModel] After RefreshGoal: HasGoal={HasGoal}, GoalProgress={GoalProgress}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();

            // Unsubscribe from all events to prevent memory leaks
            _tracker.StateChanged -= OnTrackerStateChanged;
            GoalService.GoalChanged -= OnGoalChanged;

            GC.SuppressFinalize(this);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class WeeklyUsageItem : INotifyPropertyChanged
    {
        public string Day { get; set; } = string.Empty;

        private string _usage = string.Empty;
        public string Usage
        {
            get => _usage;
            set
            {
                if (_usage == value) return;
                _usage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Usage)));
            }
        }

        private int _minutes;
        public int Minutes
        {
            get => _minutes;
            set
            {
                if (_minutes == value) return;
                _minutes = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Minutes)));
            }
        }

        public int Seconds { get; set; }

        private double _percentage;
        public double Percentage
        {
            get => _percentage;
            set
            {
                if (Math.Abs(_percentage - value) < 0.01) return;
                _percentage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Percentage)));
            }
        }

        public bool IsToday { get; set; }

        public DateTime Date { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
