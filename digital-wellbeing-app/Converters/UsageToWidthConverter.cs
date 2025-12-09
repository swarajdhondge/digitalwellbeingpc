using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace digital_wellbeing_app.Converters
{
    /// <summary>
    /// Converts usage string (e.g., "2 hr 30 min" or "0h 7m") to a proportional width.
    /// Uses square root scaling so small values are visible.
    /// </summary>
    public class UsageToWidthConverter : IValueConverter
    {
        private const double MaxWidth = 300;
        // Reference: 8 hours = full bar
        private const double MaxMinutes = 8 * 60;
        private const double MinVisibleWidth = 8;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0.0;

            string usage = value.ToString() ?? "";
            int totalMinutes = ParseUsageToMinutes(usage);

            if (totalMinutes <= 0) return 0.0; // No bar for 0

            // Square root scaling: makes small values more visible
            // sqrt(7) / sqrt(480) ≈ 0.12 instead of 7/480 ≈ 0.015
            double ratio = Math.Sqrt(totalMinutes) / Math.Sqrt(MaxMinutes);
            double width = ratio * MaxWidth;
            
            return Math.Max(MinVisibleWidth, Math.Min(width, MaxWidth));
        }

        private int ParseUsageToMinutes(string usage)
        {
            int hours = 0;
            int minutes = 0;

            // Match "X hr" or "Xh" format
            var hrMatch = Regex.Match(usage, @"(\d+)\s*h");
            // Match "X min" or "Xm" format (but not the 'h' in 'hr')
            var minMatch = Regex.Match(usage, @"(\d+)\s*m(?:in)?");

            if (hrMatch.Success)
                int.TryParse(hrMatch.Groups[1].Value, out hours);
            if (minMatch.Success)
                int.TryParse(minMatch.Groups[1].Value, out minutes);

            return hours * 60 + minutes;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts TimeSpan duration to a proportional width for app usage bars.
    /// Uses square root scaling so small values are visible.
    /// </summary>
    public class DurationToWidthConverter : IValueConverter
    {
        private const double MaxWidth = 200;
        // Reference: 2 hours = full bar
        private const double MaxMinutes = 2 * 60;
        private const double MinVisibleWidth = 12;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not TimeSpan duration) return 0.0;

            // Only show bar if at least 1 minute (ignore seconds-only usage)
            if (duration.TotalMinutes < 1.0) return 0.0;

            // Square root scaling for better visibility of small values
            double ratio = Math.Sqrt(duration.TotalMinutes) / Math.Sqrt(MaxMinutes);
            double width = ratio * MaxWidth;
            
            return Math.Max(MinVisibleWidth, Math.Min(width, MaxWidth));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

