using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace FactorioModManager.Views.Converters
{
    public class InstalledStatusConverter : IValueConverter
    {
        public static InstalledStatusConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool installed ? (installed ? "✅" : "❌") : "";

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class InstalledStatusColorConverter : IValueConverter
    {
        public static InstalledStatusColorConverter Instance { get; } = new();

        private static readonly SolidColorBrush LimeGreenBrush = new(Color.FromRgb(0, 255, 0));
        private static readonly SolidColorBrush OrangeRedBrush = new(Color.FromRgb(255, 69, 0));
        private static readonly SolidColorBrush GrayBrush = new(Colors.Gray);

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool installed
                ? (installed ? LimeGreenBrush : OrangeRedBrush)
                : GrayBrush;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class ButtonTextConverter : IValueConverter
    {
        public static ButtonTextConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool installing)
                return installing ? "Installing..." : "Download";

            return "Download";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class CanDownloadToVisibilityConverter : IValueConverter  // ✅ NEW
    {
        public static CanDownloadToVisibilityConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool canDownload && canDownload ? "Visible" : "Collapsed";

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
         => throw new NotImplementedException();
    }

    public class ActionButtonTextConverter : IValueConverter  // ✅ NEW
    {
        public static ActionButtonTextConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool isInstalled
                ? (isInstalled ? "Delete" : "Download")
                : "Download";

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class ActionButtonColorConverter : IValueConverter  // ✅ NEW
    {
        public static ActionButtonColorConverter Instance { get; } = new();

        private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0, 255, 0));
        private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(255, 100, 100));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool isInstalled
                ? (isInstalled ? RedBrush : GreenBrush)
                : GreenBrush;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}