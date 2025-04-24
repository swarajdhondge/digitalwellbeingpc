using System;
using System.Collections.ObjectModel;
using System.Linq;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.ViewModels
{
    public class ScreenViewModel
    {
        public ObservableCollection<WeeklyUsageItem> WeeklyUsage { get; } = new();

        public void LoadWeeklyUsage()
        {
            WeeklyUsage.Clear();
            var db = DatabaseService.GetConnection();

            DateTime monday = DateTime.Today;
            while (monday.DayOfWeek != DayOfWeek.Monday)
                monday = monday.AddDays(-1);

            for (int i = 0; i < 7; i++)
            {
                DateTime day = monday.AddDays(i);
                string dateKey = day.ToString("yyyy-MM-dd");

                var entry = db.Table<ScreenTimePeriod>()
                              .FirstOrDefault(x => x.SessionDate == dateKey);

                int seconds = entry?.AccumulatedActiveSeconds ?? 0;
                var ts = TimeSpan.FromSeconds(seconds);

                WeeklyUsage.Add(new WeeklyUsageItem
                {
                    Day = day.DayOfWeek.ToString(),
                    Usage = $"{(int)ts.TotalHours} hr {ts.Minutes} min"
                });
            }
        }
    }

    public class WeeklyUsageItem
    {
        public string Day { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
    }
}
