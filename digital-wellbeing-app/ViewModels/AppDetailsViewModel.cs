using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media.Imaging;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.ViewModels
{
    public class AppDetailsViewModel : INotifyPropertyChanged
    {
        private string _executablePath = string.Empty;
        private string _appName = string.Empty;
        private BitmapImage? _appIcon;
        private string _timeUsedToday = "0 m";
        private string _longestSessionToday = "0 m";

        public string ExecutablePath
        {
            get => _executablePath;
            private set
            {
                if (_executablePath != value)
                {
                    _executablePath = value;
                    OnPropertyChanged(nameof(ExecutablePath));
                }
            }
        }

        public string AppName
        {
            get => _appName;
            private set
            {
                if (_appName != value)
                {
                    _appName = value;
                    OnPropertyChanged(nameof(AppName));
                }
            }
        }

        public BitmapImage? AppIcon
        {
            get => _appIcon;
            private set
            {
                if (_appIcon != value)
                {
                    _appIcon = value;
                    OnPropertyChanged(nameof(AppIcon));
                }
            }
        }

        public string TimeUsedToday
        {
            get => _timeUsedToday;
            private set
            {
                if (_timeUsedToday != value)
                {
                    _timeUsedToday = value;
                    OnPropertyChanged(nameof(TimeUsedToday));
                }
            }
        }

        public string LongestSessionToday
        {
            get => _longestSessionToday;
            private set
            {
                if (_longestSessionToday != value)
                {
                    _longestSessionToday = value;
                    OnPropertyChanged(nameof(LongestSessionToday));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void LoadApp(string executablePath)
        {
            try
            {
                ExecutablePath = executablePath;
                AppIcon = AppIconService.GetIconForExe(executablePath);

                // Fetch sessions for today
                var sessions = DatabaseService.GetAppUsageSessionsForDate(DateTime.Today)
                    .Where(s => string.Equals(s.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (sessions.Count > 0)
                {
                    // Resolve name from the most recent session or fallback
                    var rawName = sessions.FirstOrDefault(s => !string.IsNullOrEmpty(s.AppName))?.AppName ?? "Unknown Application";
                    AppName = AppNameService.GetDisplayName(rawName, executablePath);

                    var totalSeconds = sessions.Sum(s => s.Duration.TotalSeconds);
                    var longestSeconds = sessions.Max(s => s.Duration.TotalSeconds);

                    TimeUsedToday = TimeFormatHelper.FormatDuration(TimeSpan.FromSeconds(totalSeconds));
                    LongestSessionToday = TimeFormatHelper.FormatDuration(TimeSpan.FromSeconds(longestSeconds));
                }
                else
                {
                    // Graceful fallback if no usage today but we navigated here (e.g. from week view)
                    AppName = AppNameService.GetDisplayName(System.IO.Path.GetFileNameWithoutExtension(executablePath), executablePath);
                    TimeUsedToday = "0 m";
                    LongestSessionToday = "0 m";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppDetails] Error loading stats: {ex.Message}");
                AppName = "Application";
                TimeUsedToday = "-";
                LongestSessionToday = "-";
            }
        }
    }
}
