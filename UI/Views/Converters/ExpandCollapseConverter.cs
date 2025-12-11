using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FactorioModManager.Views.Converters
{
    public class ExpandCollapseConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is bool isExpanded && isExpanded ? "Collapse" : "Expand";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
                   => throw new NotSupportedException("One-way converter");
    }
}