using System;
using System.Collections.Generic;
using digital_wellbeing_app.Helpers;

namespace digital_wellbeing_app.Models
{
    /// <summary>
    /// Daily screen time data point for trend charts
    /// </summary>
    public class DailyScreenTime
    {
        /// <summary>The date for this data point</summary>
        public DateTime Date { get; set; }

        /// <summary>Day of week label (Mon, Tue, etc.)</summary>
        public string DayLabel => Date.ToString("ddd");

        /// <summary>Full date label for tooltips</summary>
        public string DateLabel => Date.ToString("MMM d");

        /// <summary>Total screen time in hours (for chart Y-axis)</summary>
        public double Hours { get; set; }

        /// <summary>Total screen time in seconds (for precise calculations)</summary>
        public int TotalSeconds { get; set; }

        /// <summary>Formatted display string (e.g., "5h 23m")</summary>
        public string FormattedTime => TimeFormatHelper.FormatCompact(TimeSpan.FromSeconds(TotalSeconds));
    }

    /// <summary>
    /// App usage summary for top apps breakdown
    /// </summary>
    public class AppUsageSummary
    {
        /// <summary>Display name of the app</summary>
        public string AppName { get; set; } = string.Empty;

        /// <summary>Path to executable (for icon extraction)</summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>Total usage time in seconds</summary>
        public int TotalSeconds { get; set; }

        /// <summary>Total usage time in hours (for charts)</summary>
        public double Hours => TotalSeconds / 3600.0;

        /// <summary>Percentage of total app usage</summary>
        public double Percentage { get; set; }

        /// <summary>Percentage relative to the max app (0-100 for progress bar)</summary>
        public double PercentOfMax { get; set; } = 100;

        /// <summary>App category (Work/Entertainment/Uncategorized)</summary>
        public AppCategoryType Category { get; set; } = AppCategoryType.Uncategorized;

        /// <summary>Formatted display string</summary>
        public string FormattedTime => TimeFormatHelper.FormatCompact(TimeSpan.FromSeconds(TotalSeconds));
    }

    /// <summary>
    /// Focus vs Leisure time comparison data
    /// </summary>
    public class FocusLeisureComparison
    {
        /// <summary>Time spent in focus sessions (Work apps)</summary>
        public TimeSpan FocusTime { get; set; }

        /// <summary>Time spent on entertainment apps</summary>
        public TimeSpan LeisureTime { get; set; }

        /// <summary>Time spent on uncategorized apps</summary>
        public TimeSpan OtherTime { get; set; }

        /// <summary>Total tracked time</summary>
        public TimeSpan TotalTime => FocusTime + LeisureTime + OtherTime;

        /// <summary>Focus percentage (0-100)</summary>
        public double FocusPercentage => TotalTime.TotalSeconds > 0
            ? (FocusTime.TotalSeconds / TotalTime.TotalSeconds) * 100
            : 0;

        /// <summary>Leisure percentage (0-100)</summary>
        public double LeisurePercentage => TotalTime.TotalSeconds > 0
            ? (LeisureTime.TotalSeconds / TotalTime.TotalSeconds) * 100
            : 0;

        /// <summary>Other percentage (0-100)</summary>
        public double OtherPercentage => TotalTime.TotalSeconds > 0
            ? (OtherTime.TotalSeconds / TotalTime.TotalSeconds) * 100
            : 0;

        public string FocusFormatted => TimeFormatHelper.FormatCompact(FocusTime);
        public string LeisureFormatted => TimeFormatHelper.FormatCompact(LeisureTime);
        public string OtherFormatted => TimeFormatHelper.FormatCompact(OtherTime);
    }

    /// <summary>
    /// Week-over-week comparison metrics
    /// </summary>
    public class WeekComparison
    {
        /// <summary>This week's total screen time</summary>
        public TimeSpan ThisWeekScreenTime { get; set; }

        /// <summary>Last week's total screen time</summary>
        public TimeSpan LastWeekScreenTime { get; set; }

        /// <summary>Screen time change (can be negative)</summary>
        public TimeSpan ScreenTimeChange => ThisWeekScreenTime - LastWeekScreenTime;

        /// <summary>Screen time change percentage</summary>
        public double ScreenTimeChangePercent => LastWeekScreenTime.TotalSeconds > 0
            ? ((ThisWeekScreenTime.TotalSeconds - LastWeekScreenTime.TotalSeconds) / LastWeekScreenTime.TotalSeconds) * 100
            : 0;

        /// <summary>Number of focus sessions this week</summary>
        public int ThisWeekFocusSessions { get; set; }

        /// <summary>Number of focus sessions last week</summary>
        public int LastWeekFocusSessions { get; set; }

        /// <summary>Focus session count change</summary>
        public int FocusSessionChange => ThisWeekFocusSessions - LastWeekFocusSessions;

        /// <summary>This week's total focus time</summary>
        public TimeSpan ThisWeekFocusTime { get; set; }

        /// <summary>Last week's total focus time</summary>
        public TimeSpan LastWeekFocusTime { get; set; }

        /// <summary>Whether screen time improved (decreased)</summary>
        public bool ScreenTimeImproved => ScreenTimeChange.TotalSeconds < 0;

        /// <summary>Whether focus sessions improved (increased)</summary>
        public bool FocusSessionsImproved => FocusSessionChange > 0;

        // Formatted strings for UI
        public string ThisWeekFormatted => TimeFormatHelper.FormatCompact(ThisWeekScreenTime);
        public string LastWeekFormatted => TimeFormatHelper.FormatCompact(LastWeekScreenTime);
        public string ChangeFormatted => (ScreenTimeChange.TotalSeconds >= 0 ? "+" : "-") + TimeFormatHelper.FormatCompact(ScreenTimeChange.Duration());
        public string ChangePercentFormatted => $"{(ScreenTimeChangePercent >= 0 ? "+" : "")}{ScreenTimeChangePercent:F0}%";
    }

    /// <summary>
    /// Complete weekly report data container
    /// </summary>
    public class WeeklyReportData
    {
        /// <summary>Start date of the report week (Monday)</summary>
        public DateTime WeekStart { get; set; }

        /// <summary>End date of the report week (Sunday)</summary>
        public DateTime WeekEnd { get; set; }

        /// <summary>Week label for display (e.g., "Dec 16 - Dec 22")</summary>
        public string WeekLabel => $"{WeekStart:MMM d} - {WeekEnd:MMM d}";

        /// <summary>Daily screen time trend data</summary>
        public List<DailyScreenTime> DailyTrend { get; set; } = new();

        /// <summary>Top apps by usage</summary>
        public List<AppUsageSummary> TopApps { get; set; } = new();

        /// <summary>Focus vs Leisure breakdown</summary>
        public FocusLeisureComparison FocusVsLeisure { get; set; } = new();

        /// <summary>Week-over-week comparison</summary>
        public WeekComparison WeekOverWeek { get; set; } = new();

        /// <summary>Total screen time for the week</summary>
        public TimeSpan TotalScreenTime { get; set; }

        /// <summary>Average daily screen time</summary>
        public TimeSpan AverageDailyTime => TimeSpan.FromSeconds(TotalScreenTime.TotalSeconds / 7);

        /// <summary>Number of completed focus sessions this week</summary>
        public int FocusSessionCount { get; set; }

        // Formatted display strings
        public string TotalFormatted => TimeFormatHelper.FormatCompact(TotalScreenTime);
        public string AverageFormatted => TimeFormatHelper.FormatCompact(AverageDailyTime);
    }
}

