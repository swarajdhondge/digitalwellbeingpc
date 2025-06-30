using System;
using System.Linq;
using System.Threading.Tasks;

namespace digital_wellbeing_app.Services
{
    public static class DataRetrievalService
    {
        /// <summary>
        /// Sum up all AppUsageSession durations for today.
        /// </summary>
        public static async Task<string> GetAppTimeAsync()
        {
            return await Task.Run(() =>
            {
                var sessions = DatabaseService.GetAppUsageSessionsForDate(DateTime.Today);
                // assuming AppUsageSession has StartTime and EndTime DateTime properties
                var total = sessions.Aggregate(
                    TimeSpan.Zero,
                    (sum, s) => sum + (s.EndTime - s.StartTime)
                );
                return FormatTimeSpan(total);
            });
        }

        /// <summary>
        /// Read today’s accumulated active seconds from ScreenTimePeriod.
        /// </summary>
        public static async Task<string> GetScreenTimeAsync()
        {
            return await Task.Run(() =>
            {
                var period = DatabaseService.GetScreenTimePeriodForToday();
                if (period == null)
                    return "0:00";

                // Use the AccumulatedActiveSeconds integer field
                TimeSpan total = TimeSpan.FromSeconds(period.AccumulatedActiveSeconds);
                return FormatTimeSpan(total);
            });
        }

        /// <summary>
        /// Sum up all SoundUsageSession durations for today.
        /// </summary>
        public static async Task<string> GetSoundTimeAsync()
        {
            return await Task.Run(() =>
            {
                var sessions = DatabaseService.GetSoundSessionsForDate(DateTime.Today);
                // assuming SoundUsageSession has StartTime and EndTime DateTime properties
                var total = sessions.Aggregate(
                    TimeSpan.Zero,
                    (sum, s) => sum + (s.EndTime - s.StartTime)
                );
                return FormatTimeSpan(total);
            });
        }

        /// <summary>
        /// Convert a TimeSpan into H:MM format.
        /// </summary>
        private static string FormatTimeSpan(TimeSpan ts)
            => $"{(int)ts.TotalHours}:{ts.Minutes:D2}";
    }
}
