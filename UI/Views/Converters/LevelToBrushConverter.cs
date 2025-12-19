using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using FactorioModManager.Models;

namespace FactorioModManager.Views.Converters
{
    public class LevelToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Normalize input to a single string key so we only need one switch
            var key = value switch
            {
                LogLevel lvl => lvl.ToString(),
                string s => s,
                _ => value?.ToString() ?? string.Empty
            };

            return key switch
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