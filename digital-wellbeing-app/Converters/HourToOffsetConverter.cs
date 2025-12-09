using System;
using System.Globalization;
using System.Windows.Data;

namespace digital_wellbeing_app.Converters
{
    public class HourToOffsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not int hour) return 0d;
            double width = 360;
            if (parameter != null && double.TryParse(parameter.ToString(), out double w))
                width = w;
            return (hour / 24.0) * width;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

