using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace digital_wellbeing_app.Converters
{
    /// <summary>fraction (0..1) → width in px. ConverterParameter = the track width.</summary>
    public class FractionToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double frac = value is double d ? d : 0;
            double width = 100;
            if (parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p)) width = p;
            return Math.Max(0, Math.Min(1, frac)) * width;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Inverts a boolean (true→false).</summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : true;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : true;
    }

    /// <summary>Maps false→Visible, true→Collapsed (for "show when off").</summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Converts a 0..1 progress fraction into a StrokeDashArray for drawing a ring
    /// (a stroked Ellipse). ConverterParameter is the ring circumference expressed in
    /// stroke-thickness units (= pi * (diameter - thickness) / thickness). The first dash
    /// segment is the visible arc; the gap is large enough to hide the remainder.
    /// </summary>
    public class FractionToDashConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double fraction = value is double d ? d : 0;
            fraction = Math.Clamp(fraction, 0, 1);

            double units = 32.0;
            if (parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
                units = p;

            // tiny minimum so a 0% ring shows nothing rather than a stray round-cap dot
            double dash = Math.Max(0.0001, fraction * units);
            return new DoubleCollection { dash, units * 2 };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Maps an app/category ColorIndex to one of the Pulse app-tint colours, cycling
    /// through Work / Comms / Audio / Browsing / System. Used for the dashboard's
    /// per-app segmented bar and legend dots.
    /// </summary>
    public class ColorIndexToTintBrushConverter : IValueConverter
    {
        private static readonly Color[] Tints =
        {
            (Color)ColorConverter.ConvertFromString("#5B86D6"), // Work
            (Color)ColorConverter.ConvertFromString("#8A7FD0"), // Comms
            (Color)ColorConverter.ConvertFromString("#5FA98C"), // Audio
            (Color)ColorConverter.ConvertFromString("#C77F8E"), // Browsing
            (Color)ColorConverter.ConvertFromString("#B79A6B"), // System
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int index = value is int i ? i : 0;
            if (index < 0) index = 0;
            return new SolidColorBrush(Tints[index % Tints.Length]);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
