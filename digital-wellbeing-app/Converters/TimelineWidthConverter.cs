using System;
using System.Globalization;
using System.Windows.Data;

namespace digital_wellbeing_app.Converters
{
    public class TimelineWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0;
            double.TryParse(value.ToString(), out double percent);
            double width = 360;
            if (parameter != null && double.TryParse(parameter.ToString(), out double paramWidth))
                width = paramWidth;
            return percent * width;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

