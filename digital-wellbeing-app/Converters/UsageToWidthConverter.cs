using System;
using System.Globalization;
using System.Windows.Data;

namespace digital_wellbeing_app.Converters
{
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

