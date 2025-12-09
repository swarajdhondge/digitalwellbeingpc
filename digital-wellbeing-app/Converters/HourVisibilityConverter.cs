using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace digital_wellbeing_app.Converters
{
    public class HourVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 ||
                values[0] is not int hour ||
                values[1] is not double width)
                return Visibility.Collapsed;

            const int totalHours = 24;
            const double minSpacing = 60;

            double maxLabels = Math.Max(1, Math.Floor(width / minSpacing));
            int step = (int)Math.Ceiling(totalHours / maxLabels);
            if (step < 1) step = 1;

            if (hour == 0) return Visibility.Visible;
            return (hour % step == 0) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

