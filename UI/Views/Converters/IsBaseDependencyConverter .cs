using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FactorioModManager.Views.Converters
{
    public class IsBaseDependencyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string dep)
            {
                var name = Constants.DependencyHelper.ExtractDependencyName(dep);
                return string.Equals(name, "base", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
                => throw new NotSupportedException("One-way converter");
    }
}