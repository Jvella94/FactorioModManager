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

    public record LogEntry(DateTime Timestamp, string Message, LogLevel Level);
}