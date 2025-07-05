using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace digital_wellbeing_app.ViewModels
{
    /// <summary>
    /// Given an hour (0–23) and the total available width,
    /// chooses a label that will fit without clipping:
    /// - If very narrow, hides the label.
    /// - If narrow, shows “1A”/“1P” (no colon).
    /// - Otherwise, shows “1 AM”/“12 PM” etc.
    /// - For hour 23, shows “11:59 PM” at widest.
    /// </summary>
    public class HourDynamicLabelConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2
                || values[0] is not int hour
                || !double.TryParse(values[1]?.ToString(), out double totalWidth))
            {
                return string.Empty;
            }

            const int totalHours = 24;
            // how many pixels per hour column?
            double cellWidth = totalWidth / totalHours;

            // 1) If super narrow, hide
            if (cellWidth < 25)
                return string.Empty;

            bool isAM = hour < 12;
            int displayHour = hour % 12 == 0 ? 12 : hour % 12;

            // 2) If medium narrow, show compact (e.g. "1A"/"1P")
            if (cellWidth < 40)
                return $"{displayHour}{(isAM ? "A" : "P")}";

            // 3) If a bit wider, show without minutes ("1 AM"/"12 PM")
            if (cellWidth < 60)
                return $"{displayHour} {(isAM ? "AM" : "PM")}";

            // 4) Full width: show colon; special-case 23->"11:59 PM"
            if (hour == 23)
                return "11:59 PM";
            return $"{displayHour}:00 {(isAM ? "AM" : "PM")}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
