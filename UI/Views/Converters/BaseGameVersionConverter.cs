using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FactorioModManager.Views.Converters
{
    public class BaseGameVersionConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string dep)
            {
                var parts = dep.Split(Constants.Separators.Dependency, StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > 1 ? $"v{parts[1]}" : "";
            }
            return "";
        }

        /// <summary>
        /// Not supported - this converter is one-way only
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("One-way converter");
    }
}