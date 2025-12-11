using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

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
}