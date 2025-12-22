using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Collections.Generic;
using static FactorioModManager.Constants;

namespace FactorioModManager.Views.Converters
{
    public class HasGameDependenciesConverter : IValueConverter
    {
        public static HasGameDependenciesConverter Instance { get; } = new HasGameDependenciesConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return false;

            // Expecting an IEnumerable<string> (Dependencies)
            if (value is IEnumerable<string> deps)
            {
                foreach (var raw in deps)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    var name = DependencyHelper.ExtractDependencyName(raw);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (DependencyHelper.IsGameDependency(name))
                        return true;
                }
                return false;
            }

            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("One-way converter");
    }
}
