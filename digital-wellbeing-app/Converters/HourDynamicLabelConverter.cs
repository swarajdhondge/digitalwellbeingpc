using System;
using System.Globalization;
using System.Windows.Data;

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

            bool isAM = hour < 12;
            int displayHour = hour % 12 == 0 ? 12 : hour % 12;

            if (cellWidth < 40)
                return $"{displayHour}{(isAM ? "A" : "P")}";

            if (cellWidth < 60)
                return $"{displayHour} {(isAM ? "AM" : "PM")}";

            if (hour == 23)
                return "11:59 PM";
            return $"{displayHour}:00 {(isAM ? "AM" : "PM")}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

