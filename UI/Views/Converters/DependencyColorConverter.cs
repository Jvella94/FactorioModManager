using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace FactorioModManager.Views.Converters
{
    public class DependencyColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
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