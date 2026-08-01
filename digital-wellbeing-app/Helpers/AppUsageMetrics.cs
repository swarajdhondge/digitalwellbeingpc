using System;
using System.Collections.Generic;
using System.Linq;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Helpers
{
    public readonly record struct AppFocusMetrics(int SwitchCount, TimeSpan AverageFocusTime, TimeSpan LongestFocusTime);

    /// <summary>
    /// Converts persisted app-usage chunks into user-visible focus stretches. The tracker closes
    /// and reopens the same app every five minutes for crash safety; those adjacent rows are not
    /// app switches and must be merged before calculating switch, average, and longest metrics.
    /// </summary>
    public static class AppUsageMetrics
    {
        private static readonly TimeSpan JoinTolerance = TimeSpan.FromSeconds(2);

        public static AppFocusMetrics Calculate(
            IEnumerable<AppUsageSession> persistedSessions,
            AppUsageSession? liveSession = null,
            DateTime? now = null)
        {
            var intervals = persistedSessions
                .Where(s => s.EndTime > s.StartTime)
                .Select(s => new Interval(
                    AppIdentity.NormalizeKey(s.ExecutablePath, s.AppName),
                    s.StartTime,
                    s.EndTime))
                .Where(i => i.Key.Length > 0)
                .ToList();

            if (liveSession != null)
            {
                var liveEnd = now ?? DateTime.Now;
                if (liveEnd > liveSession.StartTime)
                {
                    var key = AppIdentity.NormalizeKey(liveSession.ExecutablePath, liveSession.AppName);
                    if (key.Length > 0)
                        intervals.Add(new Interval(key, liveSession.StartTime, liveEnd));
                }
            }

            var ordered = intervals.OrderBy(i => i.Start).ThenBy(i => i.End).ToList();
            if (ordered.Count == 0)
                return new AppFocusMetrics(0, TimeSpan.Zero, TimeSpan.Zero);

            var stretches = new List<Interval>();
            foreach (var interval in ordered)
            {
                if (stretches.Count > 0)
                {
                    var previous = stretches[^1];
                    var gap = interval.Start - previous.End;
                    if (previous.Key == interval.Key && gap <= JoinTolerance)
                    {
                        stretches[^1] = previous with { End = interval.End > previous.End ? interval.End : previous.End };
                        continue;
                    }
                }

                stretches.Add(interval);
            }

            var durations = stretches.Select(s => s.End - s.Start).ToList();
            return new AppFocusMetrics(
                Math.Max(0, stretches.Count - 1),
                TimeSpan.FromSeconds(durations.Average(d => d.TotalSeconds)),
                durations.Max());
        }

        private readonly record struct Interval(string Key, DateTime Start, DateTime End);
    }
}
