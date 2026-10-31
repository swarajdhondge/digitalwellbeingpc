using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Cli
{
    /// <summary>
    /// Adds the running app's not-yet-saved time (live.json, written every 30 s) to the saved
    /// data, the same way the app adds its in-memory trackers. Without a fresh snapshot the
    /// commands show saved data only.
    /// </summary>
    public static class LiveData
    {
        public static void Connect(string dataFolder, DateTime now)
        {
            var snapshot = LiveSnapshot.ReadFresh(LiveSnapshot.PathIn(dataFolder), now);
            if (snapshot == null) return;

            LiveUsageProvider.LiveActiveTime = () =>
            {
                var saved = DatabaseService.GetScreenTimePeriodForToday()?.AccumulatedActiveSeconds ?? 0;
                return TimeSpan.FromSeconds(Math.Max(saved, snapshot.ActiveSeconds));
            };

            LiveUsageProvider.LiveAppSessionsForRange = (startDate, endDate) =>
            {
                var sessions = DatabaseService.GetAppUsageSessionsForRange(startDate, endDate).ToList();
                var live = LiveSegment(snapshot, sessions, startDate, endDate);
                if (live != null) sessions.Add(live);
                return sessions;
            };
        }

        /// <summary>
        /// The app segment the running app hasn't saved yet, clipped to the date range (mirrors
        /// AppUsageTracker.GetSessionsForRange). Null when there is none or it was saved since
        /// the snapshot was written.
        /// </summary>
        public static AppUsageSession? LiveSegment(LiveSnapshot snapshot, IEnumerable<AppUsageSession> saved,
            DateTime startDate, DateTime endDate)
        {
            if (snapshot.AppStart is not DateTime appStart || string.IsNullOrEmpty(snapshot.AppName))
                return null;

            // A periodic save after the snapshot already stored this segment.
            if (saved.Any(s => s.StartTime == appStart
                               && string.Equals(s.AppName, snapshot.AppName, StringComparison.OrdinalIgnoreCase)))
                return null;

            var start = appStart < startDate.Date ? startDate.Date : appStart;
            var end = snapshot.WrittenAt;
            var rangeEnd = endDate.Date.AddDays(1);
            if (end > rangeEnd) end = rangeEnd;
            if (end <= start) return null;

            return new AppUsageSession
            {
                AppName = snapshot.AppName,
                ExecutablePath = snapshot.ExecutablePath ?? string.Empty,
                StartTime = start,
                EndTime = end
            };
        }
    }
}
