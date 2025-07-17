using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        private string _screenTime = string.Empty;
        private string _soundTime = string.Empty;
        private string _soundHarmfulTime = string.Empty;
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
            _ = LoadTodayAsync();
        }

        private async Task LoadTodayAsync()
        {
            var today = DateTime.Today;

            // — Screen Time —
            var period = DatabaseService.GetScreenTimePeriodForToday();
            var tsScreen = period != null
                ? TimeSpan.FromSeconds(period.AccumulatedActiveSeconds)
                : TimeSpan.Zero;
            ScreenTime = Format(tsScreen);

            // — Sound Sessions —
            var soundSessions = DatabaseService.GetSoundSessionsForDate(today);

            // total listening
            var tsSound = soundSessions.Aggregate(
                TimeSpan.Zero,
                (sum, s) => sum + (s.EndTime - s.StartTime));
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
    }
}
