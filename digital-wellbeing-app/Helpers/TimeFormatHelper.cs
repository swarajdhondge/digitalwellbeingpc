using System;

namespace digital_wellbeing_app.Helpers
{
    public static class TimeFormatHelper
    {
        /// <summary>Primary display format: "4 h 20 m" (with spaces, Samsung style)</summary>
        public static string FormatDuration(TimeSpan ts)
        {
            var hours = (int)ts.TotalHours;
            var minutes = ts.Minutes;

            if (hours > 0)
                return Loc.Format("Time_HoursMinutes", hours, minutes);
            return Loc.Format("Time_Minutes", minutes);
        }

        /// <summary>Compact format: "4h 20m" (no spaces, for weekly summaries and lists)</summary>
        public static string FormatCompact(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return Loc.Format("Time_HoursMinutesCompact", (int)ts.TotalHours, ts.Minutes);
            return Loc.Format("Time_MinutesCompact", ts.Minutes);
        }

        /// <summary>Focus metrics retain seconds below one minute instead of displaying a non-zero interval as 0m.</summary>
        public static string FormatFocusMetric(TimeSpan ts)
        {
            if (ts.TotalMinutes >= 1)
                return FormatCompact(ts);

            return Loc.Format("Time_SecondsCompact", Math.Max(0, (int)Math.Round(ts.TotalSeconds)));
        }

        /// <summary>Precise format: "04:20:05" (for sound timeline)</summary>
        public static string FormatPrecise(TimeSpan ts)
            => $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";

        /// <summary>
        /// A clock time in the current culture: "9 AM" / "9:30 AM" where clocks are 12-hour,
        /// "9:00" / "21:30" where they're 24-hour.
        /// </summary>
        public static string FormatClockTime(int hour, int minute)
            => ClockTime(hour, minute, minute == 0 ? "h tt" : "h:mm tt", "H:mm");

        /// <summary>Formats with the 12-hour or 24-hour pattern, whichever the culture uses.</summary>
        public static string ClockTime(int hour, int minute, string twelveHourFormat, string twentyFourHourFormat)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            var twelveHour = culture.DateTimeFormat.ShortTimePattern.Contains('h');
            return System.DateTime.Today.AddHours(hour).AddMinutes(minute)
                .ToString(twelveHour ? twelveHourFormat : twentyFourHourFormat, culture);
        }
    }
}
