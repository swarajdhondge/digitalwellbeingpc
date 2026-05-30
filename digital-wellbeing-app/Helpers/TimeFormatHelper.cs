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

        /// <summary>Precise format: "04:20:05" (for sound timeline)</summary>
        public static string FormatPrecise(TimeSpan ts)
            => $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
