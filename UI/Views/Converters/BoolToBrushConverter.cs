using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace FactorioModManager.Views.Converters
{
    public class BoolToBrushConverter : IValueConverter
    {
        private static readonly IBrush _activeBrush = new SolidColorBrush(Color.Parse("#3545A6"));
        private static readonly IBrush _transparent = Brushes.Transparent;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return _activeBrush;
            return _transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException("One-way converter");
        }
    }
}
