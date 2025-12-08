using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FactorioModManager.Views.Converters
{
    /// <summary>
    /// Converts a dependency string to a boolean indicating whether it's NOT a game dependency.
    /// Game dependencies (base, space-age, quality, elevated-rails) return false.
    /// All other dependencies return true.
    /// </summary>
    public class IsNotGameDependencyConverter : IValueConverter
    {
        /// <summary>
        /// Converts a dependency string to a boolean indicating if it's a user mod (not a game dependency)
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string dependency)
            {
                var parts = dependency.Split(Constants.Separators.Dependency, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return true;

                var depName = parts[0];
                return !Constants.GameDependencies.IsGameDependency(depName);
            }
            return true;
        }

        /// <summary>
        /// Not supported - this converter is one-way only
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException("IsNotGameDependencyConverter is a one-way converter.");
        }
    }
}