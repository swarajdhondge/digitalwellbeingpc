using System;
using System.Collections.Generic;
using System.Linq;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Service for generating weekly report data from tracked usage
    /// </summary>
    public class ReportService
    {
        /// <summary>
        /// Get the start of the week (Monday) for a given date
        /// </summary>
        public static DateTime GetWeekStart(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
        }

        /// <summary>
        /// Get the end of the week (Sunday) for a given date
        /// </summary>
        public static DateTime GetWeekEnd(DateTime date)
        {
            return GetWeekStart(date).AddDays(6);
        }

        /// <summary>
        /// Generate complete weekly report data
        /// </summary>
        /// <param name="weekStart">Start of the week (Monday)</param>
        public WeeklyReportData GetWeeklyReport(DateTime weekStart)
        {
            var weekEnd = weekStart.AddDays(6);
            
            var report = new WeeklyReportData
            {
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                DailyTrend = GetDailyScreenTimeTrend(weekStart, weekEnd),
                TopApps = GetTopAppsForPeriod(weekStart, weekEnd),
                FocusVsLeisure = GetFocusVsLeisureTime(weekStart, weekEnd),
                WeekOverWeek = GetWeekOverWeekComparison(weekStart)
            };

            // Calculate totals
            report.TotalScreenTime = TimeSpan.FromSeconds(
                report.DailyTrend.Sum(d => d.TotalSeconds));
            
            // Count completed focus sessions
            var focusSessions = DatabaseService.GetFocusSessionsForRange(weekStart, weekEnd);
            report.FocusSessionCount = focusSessions.Count(s => s.Completed);

            return report;
        }

        /// <summary>
        /// Get daily screen time trend for the specified date range
        /// </summary>
        public List<DailyScreenTime> GetDailyScreenTimeTrend(DateTime startDate, DateTime endDate)
        {
            var periods = DatabaseService.GetScreenTimePeriodsForRange(startDate, endDate);
            var result = new List<DailyScreenTime>();

            // Create entries for each day in range (even if no data)
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var dateKey = date.ToString("yyyy-MM-dd");
                var period = periods.FirstOrDefault(p => p.SessionDate == dateKey);

                var totalSeconds = period?.AccumulatedActiveSeconds ?? 0;

                // Today's bucket must include the live session so the weekly chart's "today" bar
                // matches the Dashboard's live headline instead of lagging by the flush interval.
                if (date == DateTime.Today)
                    totalSeconds = (int)LiveUsageProvider.GetTodayActiveTime().TotalSeconds;

                result.Add(new DailyScreenTime
                {
                    Date = date,
                    TotalSeconds = totalSeconds,
                    Hours = totalSeconds / 3600.0
                });
            }

            return result;
        }

        /// <summary>
        /// Build a category lookup keyed by the canonical app identity (normalized process name).
        /// Legacy rows whose <c>AppIdentifier</c> was a full path collapse to the same key here,
        /// so the last-updated value wins on collision (safe — no ToDictionary duplicate throw).
        /// </summary>
        private static Dictionary<string, AppCategoryType> BuildCategoryLookup()
        {
            var map = new Dictionary<string, AppCategoryType>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in DatabaseService.GetAllAppCategories())
            {
                var key = AppIdentity.NormalizeKey(c.AppIdentifier);
                if (key.Length > 0)
                    map[key] = c.Category;
            }
            return map;
        }

        /// <summary>
        /// Get top apps by usage for the specified date range
        /// </summary>
        /// <param name="topCount">Number of top apps to return (default 5)</param>
        public List<AppUsageSummary> GetTopAppsForPeriod(DateTime startDate, DateTime endDate, int topCount = 5)
        {
            var sessions = DatabaseService.GetAppUsageSessionsForRange(startDate, endDate);
            var categories = BuildCategoryLookup();

            // Group by executable path and sum durations
            var appUsage = sessions
                .GroupBy(s => s.ExecutablePath)
                .Select(g =>
                {
                    var totalSeconds = (int)g.Sum(s => (s.EndTime - s.StartTime).TotalSeconds);
                    // Category keys are the canonical app identity (process name), so look up by
                    // the normalized executable path (falling back to the app name) rather than
                    // the raw full path — which never matched the stored process-name keys.
                    var categoryKey = AppIdentity.NormalizeKey(g.Key, g.First().AppName);

                    return new AppUsageSummary
                    {
                        AppName = g.First().AppName,
                        ExecutablePath = g.Key,
                        TotalSeconds = totalSeconds,
                        Category = categories.TryGetValue(categoryKey, out var cat)
                            ? cat
                            : AppCategoryType.Uncategorized
                    };
                })
                .Where(a => a.TotalSeconds > 0)
                .OrderByDescending(a => a.TotalSeconds)
                .Take(topCount)
                .ToList();

            // Calculate percentages based on total of top apps
            var totalTopSeconds = appUsage.Sum(a => a.TotalSeconds);
            var maxSeconds = appUsage.FirstOrDefault()?.TotalSeconds ?? 1;
            
            foreach (var app in appUsage)
            {
                // Use AppNameService to get proper display names
                app.AppName = AppNameService.GetDisplayName(app.AppName, app.ExecutablePath);
                
                app.Percentage = totalTopSeconds > 0 
                    ? (app.TotalSeconds / (double)totalTopSeconds) * 100 
                    : 0;
                    
                // PercentOfMax for progress bar (relative to the highest app)
                app.PercentOfMax = maxSeconds > 0 
                    ? (app.TotalSeconds / (double)maxSeconds) * 100 
                    : 0;
            }

            return appUsage;
        }

        /// <summary>
        /// Get focus vs leisure time comparison for the specified date range
        /// </summary>
        public FocusLeisureComparison GetFocusVsLeisureTime(DateTime startDate, DateTime endDate)
        {
            var sessions = DatabaseService.GetAppUsageSessionsForRange(startDate, endDate);
            var categories = BuildCategoryLookup();

            var focusSeconds = 0.0;
            var leisureSeconds = 0.0;
            var otherSeconds = 0.0;

            foreach (var session in sessions)
            {
                var duration = (session.EndTime - session.StartTime).TotalSeconds;

                // Look up by the canonical app identity, not the raw executable path.
                var categoryKey = AppIdentity.NormalizeKey(session.ExecutablePath, session.AppName);
                if (categories.TryGetValue(categoryKey, out var category))
                {
                    switch (category)
                    {
                        case AppCategoryType.Work:
                            focusSeconds += duration;
                            break;
                        case AppCategoryType.Entertainment:
                            leisureSeconds += duration;
                            break;
                        default:
                            otherSeconds += duration;
                            break;
                    }
                }
                else
                {
                    otherSeconds += duration;
                }
            }

            return new FocusLeisureComparison
            {
                FocusTime = TimeSpan.FromSeconds(focusSeconds),
                LeisureTime = TimeSpan.FromSeconds(leisureSeconds),
                OtherTime = TimeSpan.FromSeconds(otherSeconds)
            };
        }

        /// <summary>
        /// Get week-over-week comparison
        /// </summary>
        /// <param name="thisWeekStart">Start of current week (Monday)</param>
        public WeekComparison GetWeekOverWeekComparison(DateTime thisWeekStart)
        {
            var thisWeekEnd = thisWeekStart.AddDays(6);
            var lastWeekStart = thisWeekStart.AddDays(-7);
            var lastWeekEnd = thisWeekStart.AddDays(-1);

            // Get screen time for both weeks
            var thisWeekPeriods = DatabaseService.GetScreenTimePeriodsForRange(thisWeekStart, thisWeekEnd);
            var lastWeekPeriods = DatabaseService.GetScreenTimePeriodsForRange(lastWeekStart, lastWeekEnd);

            var thisWeekSeconds = thisWeekPeriods.Sum(p => p.AccumulatedActiveSeconds);
            var lastWeekSeconds = lastWeekPeriods.Sum(p => p.AccumulatedActiveSeconds);

            // Get focus sessions for both weeks
            var thisWeekFocus = DatabaseService.GetFocusSessionsForRange(thisWeekStart, thisWeekEnd);
            var lastWeekFocus = DatabaseService.GetFocusSessionsForRange(lastWeekStart, lastWeekEnd);

            var thisWeekFocusCompleted = thisWeekFocus.Where(f => f.Completed).ToList();
            var lastWeekFocusCompleted = lastWeekFocus.Where(f => f.Completed).ToList();

            return new WeekComparison
            {
                ThisWeekScreenTime = TimeSpan.FromSeconds(thisWeekSeconds),
                LastWeekScreenTime = TimeSpan.FromSeconds(lastWeekSeconds),
                ThisWeekFocusSessions = thisWeekFocusCompleted.Count,
                LastWeekFocusSessions = lastWeekFocusCompleted.Count,
                ThisWeekFocusTime = TimeSpan.FromSeconds(
                    thisWeekFocusCompleted.Sum(f => f.Duration.TotalSeconds)),
                LastWeekFocusTime = TimeSpan.FromSeconds(
                    lastWeekFocusCompleted.Sum(f => f.Duration.TotalSeconds))
            };
        }

        /// <summary>
        /// Get current week's summary for dashboard card
        /// </summary>
        public (TimeSpan totalTime, double changePercent, bool improved) GetCurrentWeekSummary()
        {
            var weekStart = GetWeekStart(DateTime.Now);
            var comparison = GetWeekOverWeekComparison(weekStart);

            return (
                comparison.ThisWeekScreenTime,
                comparison.ScreenTimeChangePercent,
                comparison.ScreenTimeImproved
            );
        }
    }
}

