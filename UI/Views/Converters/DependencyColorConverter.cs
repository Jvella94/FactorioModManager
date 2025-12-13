using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using FactorioModManager.ViewModels;

namespace FactorioModManager.Views.Converters
{
    public class DependencyColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Accept either the DependencyStatus enum or the whole DependencyViewModel for richer logic
            if (value is DependencyViewModel vm)
            {
                // Prefer explicit installed/version state
                if (vm.Status == DependencyStatus.Mandatory)
                    return Brushes.Green;

                if (!vm.IsInstalled)
                    return Brushes.DarkGoldenrod;

                // Installed but version mismatch
                if (!vm.IsVersionSatisfied)
                    return Brushes.OrangeRed;

                // Installed and satisfied
                return Brushes.DarkOliveGreen;
            }

            if (value is DependencyStatus status)
            {
                return status switch
                {
                    DependencyStatus.Mandatory => Brushes.Green,
                    DependencyStatus.OptionalInstalled => Brushes.LightGreen,
                    DependencyStatus.OptionalNotInstalled => Brushes.DarkGoldenrod,
                    DependencyStatus.Incompatible => Brushes.Red,
                    _ => Brushes.Gray
                };
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
             => throw new NotSupportedException("One-way converter");
    }

    public enum DependencyStatus
    {
        Mandatory,
        OptionalInstalled,
        OptionalNotInstalled,
        Incompatible
    }
}