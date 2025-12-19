using Avalonia.Data.Converters;
using FactorioModManager.ViewModels;
using System;
using System.Globalization;

namespace FactorioModManager.Views.Converters
{
    public class IsOptionalDependencyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Accept raw dependency string, DependencyViewModel, or DependencyStatus
            if (value is string s)
            {
                return FactorioModManager.Constants.DependencyHelper.IsOptionalDependency(s);
            }

            if (value is DependencyViewModel vm)
            {
                return vm.Status == DependencyStatus.OptionalInstalled || vm.Status == DependencyStatus.OptionalNotInstalled;
            }

            if (value is DependencyStatus status)
            {
                return status == DependencyStatus.OptionalInstalled || status == DependencyStatus.OptionalNotInstalled;
            }

            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("One-way converter");
    }
}
