using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FactorioModManager.Views.Converters
{
    public class GreaterThanFiveConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is int count && count > 5;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
                  => throw new NotSupportedException("One-way converter");
    }
}