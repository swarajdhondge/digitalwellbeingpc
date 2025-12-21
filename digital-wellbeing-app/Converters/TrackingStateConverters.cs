using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using digital_wellbeing_app.CoreLogic;

namespace digital_wellbeing_app.Converters
{
    /// <summary>
    /// Converts TrackingState to a display-friendly status text.
    /// </summary>
    public class TrackingStateToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TrackingState state)
            {
                return state switch
                {
                    TrackingState.Active => "Tracking",
                    TrackingState.Idle => "Idle",
                    TrackingState.Paused => "Paused",
                    _ => "Unknown"
                };
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts TrackingState to a status indicator color.
    /// </summary>
    public class TrackingStateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TrackingState state)
            {
                return state switch
                {
                    TrackingState.Active => new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)),   // Green
                    TrackingState.Idle => new SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 179, 8)),    // Yellow
                    TrackingState.Paused => new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)), // Red
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts goal progress (0.0 to 1.0+) to width for progress bar.
    /// </summary>
    public class GoalProgressToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 &&
                values[0] is double progress &&
                values[1] is double containerWidth)
            {
                // Cap at 100% for visual display
                var capped = Math.Min(progress, 1.0);
                return capped * containerWidth;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts goal progress to color (green if under goal, red if over).
    /// </summary>
    public class GoalProgressToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double progress)
            {
                if (progress > 1.0)
                    return new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red - over goal
                if (progress > 0.8)
                    return new SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 179, 8)); // Yellow - approaching goal
                return System.Windows.Application.Current.FindResource("Accent.Primary") as System.Windows.Media.Brush 
                       ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 102, 241)); // Primary accent
            }
            return System.Windows.Application.Current.FindResource("Accent.Primary") as System.Windows.Media.Brush 
                   ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 102, 241));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts boolean HasGoal to Visibility.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter?.ToString() == "Invert";
            bool visible = value is bool b && b;
            if (invert) visible = !visible;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts boolean to FontWeight (true = Bold, false = Normal).
    /// </summary>
    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool useBold = value is bool b && b;
            return useBold ? FontWeights.Bold : FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts percentage (0-100) and container width to actual pixel width for stacked bars.
    /// </summary>
    public class PercentageToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 &&
                values[0] is double percentage &&
                values[1] is double containerWidth)
            {
                return (percentage / 100.0) * containerWidth;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts non-empty/non-zero string to Visible, empty/"0 m" to Collapsed.
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && !string.IsNullOrEmpty(s) && s != "0 m" && s != "0 h 0 m")
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts index to segment color for stacked bars.
    /// </summary>
    public class IndexToSegmentColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int index = value is int i ? i : 0;
            return index switch
            {
                0 => System.Windows.Application.Current.FindResource("Chart.Segment1") as System.Windows.Media.Brush 
                     ?? System.Windows.Media.Brushes.Teal,
                1 => System.Windows.Application.Current.FindResource("Chart.Segment2") as System.Windows.Media.Brush 
                     ?? System.Windows.Media.Brushes.Blue,
                2 => System.Windows.Application.Current.FindResource("Chart.Segment3") as System.Windows.Media.Brush 
                     ?? System.Windows.Media.Brushes.Purple,
                _ => System.Windows.Media.Brushes.Gray
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a percentage (0-100) to a GridLength for grid column widths.
    /// </summary>
    public class PercentToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percent)
            {
                // Minimum width of 0 star, maximum of the percentage
                var stars = Math.Max(0, percent);
                return new System.Windows.GridLength(stars, System.Windows.GridUnitType.Star);
            }
            return new System.Windows.GridLength(0, System.Windows.GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
