using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media.Imaging;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.ViewModels
{
    public class AppUsageViewModel
    {
        public ObservableCollection<AppUsageSummary> TodaysUsage { get; set; }

        public AppUsageViewModel()
        {
            var sessions = DatabaseService.GetAppUsageSessionsForDate(DateTime.Now);

            // group by (AppName, ExecutablePath) in case same app name has different paths
            var grouped = sessions
                .GroupBy(s => new { s.AppName, s.ExecutablePath })
                .Select(g => new AppUsageSummary
                {
                    AppName = g.Key.AppName,
                    ExecutablePath = g.Key.ExecutablePath,
                    TotalDuration = TimeSpan.FromSeconds(g.Sum(s => s.Duration.TotalSeconds))
                })
                .OrderByDescending(x => x.TotalDuration)
                .ToList();

            TodaysUsage = new ObservableCollection<AppUsageSummary>(grouped);
        }
    }

    public class AppUsageSummary
    {
        public string AppName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public TimeSpan TotalDuration { get; set; }

        public string DurationFormatted => $"{(int)TotalDuration.TotalHours}h {TotalDuration.Minutes}m";

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
    }
}
