using Avalonia.Data.Converters;
using FactorioModManager.ViewModels;
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
            var modName = string.Empty;
            if (value is string) modName = value as string;
            if (value is DependencyViewModel) modName = (value as DependencyViewModel)!.Name;
            if (string.IsNullOrEmpty(modName)) return true;
            var parts = modName.Split(Constants.Separators.Dependency, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return true;

            var depName = parts[0];
            bool isUserMod = !Constants.DependencyHelper.IsGameDependency(depName);

            // If parameter == "invert", return the opposite (true for game deps)
            if (parameter is string p && string.Equals(p, "invert", StringComparison.OrdinalIgnoreCase))
                return !isUserMod;

            return isUserMod;
        }

        /// <summary>
        /// Not supported - this converter is one-way only
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("One-way converter");
    }
}