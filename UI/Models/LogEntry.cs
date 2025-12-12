using System;
using System.Globalization;
using Avalonia.Media;

namespace FactorioModManager.Models
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public record MessageSegment(string Text, IBrush? Foreground, FontWeight Weight = FontWeight.Normal);

    public partial class LogEntry()
    {
        public DateTime Timestamp { get; init; }
        public LogLevel Level { get; init; }
        public string Message { get; init; } = string.Empty;

        // Computed UI properties
        public string TimestampFormatted => Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        public string LevelText => Level.ToString();

        public IBrush LevelBrush => Level switch
        {
            LogLevel.Error => Brushes.Red!,
            LogLevel.Warning => Brushes.Orange!,
            LogLevel.Debug => Brushes.Cyan!,
            _ => Brushes.White!
        };
    }
}