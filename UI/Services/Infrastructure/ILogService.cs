using FactorioModManager.Models;
using System;
using System.Collections.Generic;

namespace FactorioModManager.Services.Infrastructure
{
    public interface ILogService
    {
        void Log(string message, LogLevel level = LogLevel.Info);

        void LogDebug(string message);

        void LogWarning(string message);

        void LogError(string message, Exception exception);

        void LogException(Exception exception);

        IEnumerable<LogEntry> GetLogs();

        string GetLogFilePath();

        void ClearLogs();

        void ArchiveLogs();

        void PruneOldLogs(int daysToKeep = 30);
    }
}