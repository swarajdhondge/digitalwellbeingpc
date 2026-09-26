using System;
using System.Collections.Generic;
using System.Linq;
using digital_wellbeing_app.CoreLogic;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Services
{
    /// <summary>One app's total time today (persisted rows plus the live session).</summary>
    public readonly record struct TodayAppEntry(string AppName, string ExecutablePath, TimeSpan Duration);

    /// <summary>
    /// Single source of truth for "today so far". Every view used to compute today's totals
    /// differently — the App Usage page added the in-memory current session while the Dashboard
    /// read only persisted rows, and the Dashboard's own weekly "today" bar lagged its live
    /// headline by up to the 5-minute flush interval. This provider combines persisted data with
    /// the trackers' live session in one place so all readers agree.
    ///
    /// The running trackers are read from the App singleton; when it is unavailable (unit tests,
    /// design time) the combine helpers fall back to persisted-only data.
    /// </summary>
    public static class LiveUsageProvider
    {
        private static AppUsageTracker? AppTracker
            => (System.Windows.Application.Current as App)?.AppTracker;

        private static ScreenTimeTracker? ScreenTracker
            => (System.Windows.Application.Current as App)?.ScreenTracker;

        private static SoundExposureManager? SoundTracker
            => (System.Windows.Application.Current as App)?.SoundExposureMgr;

        // --- Screen time ---

        /// <summary>Today's active screen time, live session included (persisted fallback).</summary>
        public static TimeSpan GetTodayActiveTime()
            => CombineTodayActiveTime(ScreenTracker?.CurrentActiveTime);

        /// <summary>
        /// Testable core: returns the live active time when supplied, otherwise the persisted
        /// today total. The tracker's CurrentActiveTime already includes persisted-today time, so
        /// it is authoritative when present.
        /// </summary>
        public static TimeSpan CombineTodayActiveTime(TimeSpan? liveActiveTime)
        {
            if (liveActiveTime.HasValue)
                return liveActiveTime.Value;

            var persisted = DatabaseService.GetScreenTimePeriodForToday()?.AccumulatedActiveSeconds ?? 0;
            return TimeSpan.FromSeconds(persisted);
        }

        // --- App usage ---

        /// <summary>Per-app durations for today, persisted rows plus the live current session.</summary>
        public static List<TodayAppEntry> GetTodayAppEntries()
            => GroupAppEntries(GetAppSessionsForRange(DateTime.Today, DateTime.Today));

        /// <summary>Total app time today, live session included.</summary>
        public static TimeSpan GetTodayAppTime()
            => GetTodayAppEntries().Aggregate(TimeSpan.Zero, (sum, e) => sum + e.Duration);

        /// <summary>
        /// Persisted app sessions for an inclusive local-date range plus the current in-memory
        /// session when that range contains today. Used by current-week reports so they do not
        /// lag the App Usage and Dashboard pages by the periodic-save interval.
        /// </summary>
        public static List<AppUsageSession> GetAppSessionsForRange(DateTime startDate, DateTime endDate)
            => AppTracker?.GetSessionsForRange(startDate, endDate)
                ?? DatabaseService.GetAppUsageSessionsForRange(startDate, endDate).ToList();

        /// <summary>
        /// Testable core: today's persisted app sessions grouped per app, with an optional live
        /// session folded into the matching app (or added if new). Sorted longest-first.
        /// </summary>
        public static List<TodayAppEntry> CombineTodayAppEntries(AppUsageSession? liveSession)
        {
            var sessions = DatabaseService.GetAppUsageSessionsForDate(DateTime.Today).ToList();
            // A caller may still hold the segment that a periodic save just closed.
            if (liveSession != null && liveSession.EndTime == default)
            {
                var now = DateTime.Now;
                var start = liveSession.StartTime < now.Date ? now.Date : liveSession.StartTime;
                if (now > start)
                    sessions.Add(new AppUsageSession
                    {
                        AppName = liveSession.AppName,
                        ExecutablePath = liveSession.ExecutablePath,
                        StartTime = start,
                        EndTime = now
                    });
            }
            return GroupAppEntries(sessions);
        }

        private static List<TodayAppEntry> GroupAppEntries(IEnumerable<AppUsageSession> sessions)
            => sessions.GroupBy(s => AppIdentity.NormalizeKey(s.ExecutablePath, s.AppName), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Key.Length > 0)
                .Select(g => new TodayAppEntry(g.First().AppName, g.First().ExecutablePath,
                    TimeSpan.FromTicks(g.Sum(s => s.Duration.Ticks))))
                .OrderByDescending(e => e.Duration)
                .ToList();

        // --- Sound exposure ---

        /// <summary>Today's actual listening and harmful exposure, including the live segment.</summary>
        public static (TimeSpan Listening, TimeSpan Harmful) GetTodaySoundTotals()
        {
            var sessions = DatabaseService.GetSoundSessionsForDate(DateTime.Today);
            var listening = sessions.Aggregate(TimeSpan.Zero, (sum, s) => sum + s.ActualListeningDuration);
            var harmful = sessions.Aggregate(TimeSpan.Zero, (sum, s) => sum + s.HarmfulDuration);

            var live = SoundTracker?.CurrentSession;
            if (live != null && live.StartTime.Date == DateTime.Today)
            {
                listening += live.ActualListeningDuration;
                harmful += live.HarmfulDuration;
            }

            return (listening, harmful);
        }
    }
}
