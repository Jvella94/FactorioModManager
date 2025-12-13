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
                var parsed = Constants.DependencyHelper.ParseDependency(dep);
                if (parsed != null && !string.IsNullOrEmpty(parsed.Value.Version))
                {
                    return $"v{parsed.Value.Version.Trim()}";
                }

                // Fallback: try previous split approach but trim tokens
                var parts = dep.Split(Constants.Separators.Dependency, StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > 1 ? $"v{parts[1].Trim()}" : "";
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