using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace digital_wellbeing_app.ViewModels
{
    /// <summary>
    /// Shows only every Nᵗʰ hour label so labels never overlap.
    /// </summary>
    public class HourVisibilityConverter : IMultiValueConverter
    {
        // values[0] = hour (int), values[1] = available width (double)
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 ||
                values[0] is not int hour ||
                values[1] is not double width)
                return Visibility.Collapsed;

            const int totalHours = 24;      // labels 0..23
            const double minSpacing = 60;   // px per label before thinning

            double maxLabels = Math.Max(1, Math.Floor(width / minSpacing));
            int step = (int)Math.Ceiling(totalHours / maxLabels);
            if (step < 1) step = 1;

            // Always show 0 (12 AM)
            if (hour == 0) return Visibility.Visible;
            return (hour % step == 0) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
