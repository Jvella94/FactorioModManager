using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace FactorioModManager.Views.Converters
{
    public class LevelToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var s = value?.ToString() ?? string.Empty;
            return s switch
            {
                "Error" => Brushes.Red,
                "Warning" => Brushes.Orange,
                "Debug" => Brushes.Cyan,
                "Info" => Brushes.LightGray,
                "All" => Brushes.Gray,
                _ => Brushes.White
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}