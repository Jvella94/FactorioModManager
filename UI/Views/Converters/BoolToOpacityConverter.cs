using System;
using Avalonia.Data.Converters;
using System.Globalization;

namespace FactorioModManager.Views.Converters
{
    public class BoolToOpacityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return 1.0;
            return 0.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double d)
                return d > 0.5;
            return false;
        }
    }
}
