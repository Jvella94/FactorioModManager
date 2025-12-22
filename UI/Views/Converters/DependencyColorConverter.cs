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
                // Ensure incompatible dependencies are shown distinctly
                if (vm.Status == DependencyStatus.Incompatible)
                    return Brushes.Red;

                // Prefer explicit installed/version state for mandatory and optional
                if (vm.Status == DependencyStatus.Mandatory)
                    return Brushes.Green;

                if (vm.Status == DependencyStatus.OptionalInstalled)
                    return Brushes.LightGreen;

                if (vm.Status == DependencyStatus.OptionalNotInstalled)
                    return Brushes.DarkGoldenrod;

                // Installed but version mismatch
                if (vm.IsInstalled && !vm.IsVersionSatisfied)
                    return Brushes.OrangeRed;

                // Installed and satisfied
                if (vm.IsInstalled)
                    return Brushes.DarkOliveGreen;

                return Brushes.Transparent;
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