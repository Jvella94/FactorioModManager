using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace FactorioModManager.Views.Converters
{
    public static class VersionStatusConverters
    {
        public static readonly IValueConverter ToStatusText = new FuncValueConverter<bool, string>(
            installed => installed ? "Installed" : "Not Installed");

        public static readonly IValueConverter ToStatusBrush = new FuncValueConverter<bool, IBrush>(
            installed => installed
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))  // Green
                : new SolidColorBrush(Color.FromRgb(244, 67, 54))); // Red

        public static readonly IValueConverter ToBold = new FuncValueConverter<bool, FontWeight>(
            installed => installed ? FontWeight.Bold : FontWeight.Normal);

        public static readonly IValueConverter ToActionText = new FuncValueConverter<bool, string>(
            installed => installed ? "Reinstall" : "Download");

        public static readonly IValueConverter ToActionBackground = new FuncValueConverter<bool, IBrush>(
            installed => installed
                ? new SolidColorBrush(Color.FromRgb(255, 152, 0))  // Orange
                : new SolidColorBrush(Color.FromRgb(33, 150, 243))); // Blue
    }
}
