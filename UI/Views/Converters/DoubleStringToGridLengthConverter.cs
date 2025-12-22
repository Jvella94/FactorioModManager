using System;
using Avalonia.Data.Converters;
using System.Globalization;
using Avalonia.Controls;

namespace FactorioModManager.Views.Converters
{
    public class DoubleStringToGridLengthConverter : IValueConverter
    {
        // Convert double (or string) -> GridLength (pixels)
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            double pixels = 0;
            if (value is double d)
                pixels = d;
            else if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                pixels = parsed;

            return new GridLength(pixels, GridUnitType.Pixel);
        }

        // ConvertBack GridLength -> double
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is GridLength gl)
                return gl.Value;

            if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            if (value is double dd)
                return dd;

            if (double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var f))
                return f;

            return 0.0;
        }
    }
}
