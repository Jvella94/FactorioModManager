using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using System.Collections;

namespace FactorioModManager.Views.Converters
{
    public class ActionButtonTextConverter : IValueConverter  // ✅ NEW
    {
        public static ActionButtonTextConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool isInstalled
                ? (isInstalled ? "Delete" : "Download")
                : "Download";

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("One-way converter");
    }

    public class ActionButtonColorConverter : IValueConverter  // ✅ NEW
    {
        public static ActionButtonColorConverter Instance { get; } = new();

        private static readonly SolidColorBrush _greenBrush = new(Color.FromRgb(60, 120, 60));
        private static readonly SolidColorBrush _orangeRedBrush = new(Color.FromRgb(255, 69, 0));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool isInstalled
                ? (isInstalled ? _orangeRedBrush : _greenBrush)
                : _greenBrush;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("One-way converter");
    }

    // Converter to map an operation-in-progress flag to an opacity value for graying UI
    public class OperationOpacityConverter : IValueConverter
    {
        public static OperationOpacityConverter Instance { get; } = new();

        // If an operation is in progress (true) return 0.5 to gray; otherwise 1.0
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isInProgress && isInProgress)
                return 0.5;
            return 1.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("One-way converter");
    }

    // Converter that inspects an IEnumerable (Releases) and returns true when none are installing
    public class ReleasesNotInstallingBoolConverter : IValueConverter
    {
        public static ReleasesNotInstallingBoolConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is IEnumerable items)
            {
                foreach (var it in items)
                {
                    if (it == null) continue;
                    var prop = it.GetType().GetProperty("IsInstalling");
                    if (prop != null && prop.GetValue(it) is bool b && b)
                        return false;
                }
                return true;
            }
            return true;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("One-way converter");
    }

    // Converter that returns opacity based on whether any release is installing
    public class ReleasesAnyInstallingToOpacityConverter : IValueConverter
    {
        public static ReleasesAnyInstallingToOpacityConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is IEnumerable items)
            {
                foreach (var it in items)
                {
                    if (it == null) continue;
                    var prop = it.GetType().GetProperty("IsInstalling");
                    if (prop != null && prop.GetValue(it) is bool b && b)
                        return 0.5;
                }
                return 1.0;
            }
            return 1.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("One-way converter");
    }
}