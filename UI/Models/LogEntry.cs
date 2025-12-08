using System;

namespace FactorioModManager.Models
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Debug
    }

    public record LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; } = string.Empty;
        public LogLevel Level { get; set; }
    }
}