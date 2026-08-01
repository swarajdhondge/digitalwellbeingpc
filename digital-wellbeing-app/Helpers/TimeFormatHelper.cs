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
                return $"{hours} h {minutes} m";
            return $"{minutes} m";
        }

        /// <summary>Compact format: "4h 20m" (no spaces, for weekly summaries and lists)</summary>
        public static string FormatCompact(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m";
        }

        /// <summary>Focus metrics retain seconds below one minute instead of displaying a non-zero interval as 0m.</summary>
        public static string FormatFocusMetric(TimeSpan ts)
        {
            if (ts.TotalMinutes >= 1)
                return FormatCompact(ts);

            return $"{Math.Max(0, (int)Math.Round(ts.TotalSeconds))}s";
        }

        /// <summary>Precise format: "04:20:05" (for sound timeline)</summary>
        public static string FormatPrecise(TimeSpan ts)
            => $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
