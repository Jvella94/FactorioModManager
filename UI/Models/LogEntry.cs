using System;
using System.Globalization;

namespace FactorioModManager.Models
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public partial class LogEntry()
    {
        public DateTime Timestamp { get; init; }
        public LogLevel Level { get; init; }
        public string Message { get; init; } = string.Empty;

        // Computed UI properties
        public string TimestampFormatted => Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}