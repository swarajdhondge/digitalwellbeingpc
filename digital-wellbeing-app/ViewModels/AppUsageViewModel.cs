using System;
using System.Collections.ObjectModel;
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

        #endregion

        public AppUsageViewModel()
        {
            _tracker = (System.Windows.Application.Current as App)?.AppTracker
                       ?? new AppUsageTracker();

            // Subscribe to app switch events
            _tracker.OnAppSwitched += OnAppSwitched;

            // Timer for live updates (1 second interval)
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateAll();
            _timer.Start();

            // Initial load
            LoadTodaysUsage();
            UpdateAll();
        }

        private void OnAppSwitched()
        {
            // When app switches, reload the usage list
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher?.CheckAccess() == true)
            {
                LoadTodaysUsage();
                UpdateCurrentApp();
                UpdateFocusStats();
            }
            else
            {
                dispatcher?.Invoke(() =>
                {
                    LoadTodaysUsage();
                    UpdateCurrentApp();
                    UpdateFocusStats();
                });
            }
        }

        private void UpdateAll()
        {
            if (_disposed) return;

            UpdateCurrentApp();
            UpdateFocusStats();
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
                    CurrentAppIcon = AppIconService.GetIconForExe(session.ExecutablePath);
                }

                IsTracking = true;
            }
            else
            {
                CurrentAppName = "No app active";
                CurrentWindowTitle = string.Empty;
                CurrentAppDuration = "—";
                IsTracking = false;
            }
        }

        private string? _currentIconPath;

        private void UpdateFocusStats()
        {
            var sessions = DatabaseService.GetAppUsageSessionsForDate(DateTime.Now);

            // Include current session in calculations
            var currentSession = _tracker.CurrentSession;
            var allSessions = sessions.ToList();

            // Switch count = number of distinct sessions today
            SwitchCount = allSessions.Count + (currentSession != null ? 1 : 0);

            if (allSessions.Count == 0 && currentSession == null)
            {
                AverageFocusTime = "0m";
                LongestSessionTime = "0m";
                return;
            }

            // Calculate durations
            var durations = allSessions.Select(s => s.Duration).ToList();
            
            // Add current session duration
            if (currentSession != null)
            {
                durations.Add(DateTime.Now - currentSession.StartTime);
            }

            // Longest session
            var longest = durations.Max();
            LongestSessionTime = TimeFormatHelper.FormatCompact(longest);

            // Average focus time
            var avgSeconds = durations.Average(d => d.TotalSeconds);
            AverageFocusTime = TimeFormatHelper.FormatCompact(TimeSpan.FromSeconds(avgSeconds));
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
