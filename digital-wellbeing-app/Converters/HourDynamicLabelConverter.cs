using System;
using System.Globalization;
using System.Windows.Data;
using digital_wellbeing_app.Helpers;

namespace digital_wellbeing_app.Converters
{
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
            double cellWidth = totalWidth / totalHours;

            if (cellWidth < 25)
                return string.Empty;

            if (cellWidth < 40)
                return TimeFormatHelper.ClockTime(hour, 0, "ht", "%H");

            if (cellWidth < 60)
                return TimeFormatHelper.ClockTime(hour, 0, "h tt", "H:mm");

            // The last label marks the end of the day rather than 11 PM.
            return hour == 23
                ? TimeFormatHelper.ClockTime(23, 59, "h:mm tt", "H:mm")
                : TimeFormatHelper.ClockTime(hour, 0, "h:mm tt", "H:mm");
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

