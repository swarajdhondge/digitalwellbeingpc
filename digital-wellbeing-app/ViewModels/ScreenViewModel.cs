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

        private readonly ScreenTimeTracker _tracker;
        private readonly GoalService _goalService;
        private readonly DispatcherTimer _timer;
        private bool _disposed;

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

        #endregion

        public ScreenViewModel()
        {
            _tracker = (System.Windows.Application.Current as App)?.ScreenTracker
                       ?? new ScreenTimeTracker();
            _goalService = new GoalService();

            // Subscribe to state changes
            _tracker.StateChanged += (s, state) => TrackingState = state;
            TrackingState = _tracker.State;

            // Subscribe to goal changes from Settings
            GoalService.GoalChanged += OnGoalChanged;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateAll();
            _timer.Start();

            LoadWeeklyUsage();
            UpdateAll();
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

            UpdateTodayUsage();
            UpdateTrackingState();
            UpdateContextLine();
            UpdateQuickStats();
            UpdateGoalProgress();
            UpdateTodayInWeeklyView();
        }

        private void UpdateTodayUsage()
        {
            var ts = _tracker.CurrentActiveTime;
            int totalSec = (int)ts.TotalSeconds;
            TodayTimeText = $"{totalSec / 3600} hr {(totalSec % 3600) / 60} min";

            UpdateTimelineSegments();
        }

        private void UpdateTrackingState()
        {
            TrackingState = _tracker.State;
            SessionCount = _tracker.SessionCount;
        }

        private void UpdateContextLine()
        {
            var db = DatabaseService.GetConnection();
            var yesterdayKey = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
            var yesterdayEntry = db.Table<ScreenTimePeriod>()
                                   .FirstOrDefault(x => x.SessionDate == yesterdayKey);

            var todayMinutes = (int)_tracker.CurrentActiveTime.TotalMinutes;
            var yesterdayMinutes = (yesterdayEntry?.AccumulatedActiveSeconds ?? 0) / 60;

            if (yesterdayMinutes == 0)
            {
                ContextLine = "No data from yesterday";
                return;
            }

            var diff = todayMinutes - yesterdayMinutes;
            if (diff > 0)
            {
                var diffHours = Math.Abs(diff) / 60;
                var diffMins = Math.Abs(diff) % 60;
                ContextLine = diffHours > 0
                    ? $"↑ {diffHours}h {diffMins}m more than yesterday"
                    : $"↑ {diffMins}m more than yesterday";
            }
            else if (diff < 0)
            {
                var diffHours = Math.Abs(diff) / 60;
                var diffMins = Math.Abs(diff) % 60;
                ContextLine = diffHours > 0
                    ? $"↓ {diffHours}h {diffMins}m less than yesterday"
                    : $"↓ {diffMins}m less than yesterday";
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

            // Format longest session display
            if (longestSec >= 3600)
            {
                var hours = longestSec / 3600;
                var mins = (longestSec % 3600) / 60;
                LongestSession = $"{hours}h {mins}m";
            }
            else if (longestSec >= 60)
            {
                LongestSession = $"{longestSec / 60} min";
            }
            else
            {
                LongestSession = $"{longestSec} sec";
            }

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
                var currentTime = _tracker.CurrentActiveTime;
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
        /// Update just today's entry in the weekly view (called every second)
        /// </summary>
        private void UpdateTodayInWeeklyView()
        {
            var todayItem = WeeklyUsage.FirstOrDefault(x => x.IsToday);
            if (todayItem == null) return;

            var ts = _tracker.CurrentActiveTime;
            var newUsage = $"{(int)ts.TotalHours} hr {ts.Minutes} min";
            var newMinutes = (int)ts.TotalMinutes;

            if (todayItem.Usage != newUsage)
            {
                todayItem.Usage = newUsage;
                todayItem.Minutes = newMinutes;
                
                // Recalculate weekly average
                var totalMinutes = WeeklyUsage.Sum(x => x.Minutes);
                var daysWithData = WeeklyUsage.Count(x => x.Minutes > 0);
                if (daysWithData > 0)
                {
                    WeeklyAverageMinutes = totalMinutes / daysWithData;
                    var avgHours = WeeklyAverageMinutes / 60;
                    var avgMins = WeeklyAverageMinutes % 60;
                    WeeklyAverageText = $"{avgHours} hr {avgMins} min";
                }

                OnPropertyChanged(nameof(WeeklyUsage));
            }
        }

        public void LoadWeeklyUsage()
        {
            if (_disposed) return;
            WeeklyUsage.Clear();
            var db = DatabaseService.GetConnection();

            DateTime monday = DateTime.Today;
            while (monday.DayOfWeek != DayOfWeek.Monday)
                monday = monday.AddDays(-1);

            int totalMinutes = 0;
            int daysWithData = 0;

            for (int i = 0; i < 7; i++)
            {
                var day = monday.AddDays(i);
                var key = day.ToString("yyyy-MM-dd");
                var entry = db.Table<ScreenTimePeriod>()
                              .FirstOrDefault(x => x.SessionDate == key);
                
                int sec = entry?.AccumulatedActiveSeconds ?? 0;
                
                // For today, use live data
                if (day.Date == DateTime.Today)
                    sec = (int)_tracker.CurrentActiveTime.TotalSeconds;

                var ts = TimeSpan.FromSeconds(sec);
                bool isToday = day.Date == DateTime.Today;

                if (sec > 0)
                {
                    totalMinutes += (int)ts.TotalMinutes;
                    daysWithData++;
                }

                WeeklyUsage.Add(new WeeklyUsageItem
                {
                    Day = day.DayOfWeek.ToString(),
                    Usage = $"{(int)ts.TotalHours} hr {ts.Minutes} min",
                    Minutes = (int)ts.TotalMinutes,
                    IsToday = isToday
                });
            }

            // Calculate weekly average
            if (daysWithData > 0)
            {
                WeeklyAverageMinutes = totalMinutes / daysWithData;
                var avgHours = WeeklyAverageMinutes / 60;
                var avgMins = WeeklyAverageMinutes % 60;
                WeeklyAverageText = $"{avgHours} hr {avgMins} min";
            }
            else
            {
                WeeklyAverageMinutes = 0;
                WeeklyAverageText = "0 hr 0 min";
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

            // 2) Live session segment – use actual session start time
            var sessionStart = _tracker.CurrentSessionStart;
            var sessionSeconds = _tracker.CurrentSessionSeconds;
            if (sessionStart.HasValue && sessionSeconds > 0)
            {
                // Only show if session started today
                if (sessionStart.Value.Date == DateTime.Today)
                {
                    var startSecs = sessionStart.Value.TimeOfDay.TotalSeconds;
                    var widthSecs = sessionSeconds;

                    TimelineSegments.Add(new ScreenTimelineSegment
                    {
                        StartPercent = startSecs / daySecs,
                        WidthPercent = Math.Min(1.0, widthSecs / daySecs)
                    });
                }
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
            
            // Unsubscribe from static event to prevent memory leaks
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
        
        public bool IsToday { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
