using System;
using System.Globalization;

namespace digital_wellbeing_app.Helpers
{
    /// <summary>Shared Monday-to-Sunday week calculations used by every history page.</summary>
    public static class WeekNavigationHelper
    {
        public static DateTime StartOfWeek(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
        }

        public static string FormatWeek(DateTime weekStart, bool includeWeekNumber = true)
        {
            weekStart = StartOfWeek(weekStart);
            var weekEnd = weekStart.AddDays(6);
            var range = weekStart.Month == weekEnd.Month
                ? $"{weekStart:MMM d}\u2013{weekEnd:d}"
                : $"{weekStart:MMM d}\u2013{weekEnd:MMM d}";

            if (!includeWeekNumber)
                return range;

            var weekNumber = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                weekStart,
                CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
            return $"W{weekNumber} \u00B7 {range}";
        }

        public static DateTime Clamp(DateTime requested, DateTime? earliestWeekStart)
        {
            var selected = StartOfWeek(requested);
            var current = StartOfWeek(DateTime.Today);
            if (selected > current) selected = current;
            if (earliestWeekStart.HasValue && selected < StartOfWeek(earliestWeekStart.Value))
                selected = StartOfWeek(earliestWeekStart.Value);
            return selected;
        }
    }
}
