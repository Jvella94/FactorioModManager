using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Globalization;

namespace FactorioModManager.Views.Converters
{
    public class GameDependencyIconPathConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string dep)
                return null;

            var name = Constants.DependencyHelper.ExtractDependencyName(dep);

            var key = name switch
            {
                "base" => "base",
                "space-age" => "space-age",
                "quality" => "quality",
                "elevated-rails" => "elevated",
                _ => null
            };

            if (key is null)
                return null;

            var uri = new Uri($"avares://FactorioModManager/Assets/{key}.png");

            try
            {
                var assets = AssetLoader.Open(uri);
                return new Bitmap(assets);
            }
            catch (Exception ex)
            {
                var logService = ServiceContainer.Instance.Resolve<ILogService>();
                logService?.LogError($"Failed to load Game Icon Bitmap {ex.Message}", ex);
                return null;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}