using FactorioModManager.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace FactorioModManager.Services.Infrastructure
{
    public class LogService : ILogService
    {
        private static readonly Lazy<LogService> _instance = new(() => new LogService());
        public static LogService Instance => _instance.Value;

        private readonly ConcurrentBag<LogEntry> _logs = [];
        private readonly string _logFilePath;
        private readonly Lock _fileLock = new();

        public LogService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FactorioModManager");

            Directory.CreateDirectory(appDataPath);
            _logFilePath = Path.Combine(appDataPath, "application.log");

            LoadLogsFromFile();
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            LogInternal(message, level);
        }

        public void LogDebug(string message) => LogInternal(message, LogLevel.Debug);

        public void LogWarning(string message) => LogInternal(message, LogLevel.Warning);

        public void LogError(string message) => LogInternal(message, LogLevel.Error);

        private void LogInternal(string message, LogLevel level)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            };

            _logs.Add(entry);
            WriteToFile(entry);
        }

        private void WriteToFile(LogEntry entry)
        {
            try
            {
                using (_fileLock.EnterScope())
                {
                    var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Message}";
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Intentionally ignore logging failures
            }
        }

        private void LoadLogsFromFile()
        {
            try
            {
                if (!File.Exists(_logFilePath))
                    return;

                lock (_fileLock)
                {
                    var lines = File.ReadAllLines(_logFilePath);
                    foreach (var line in lines)
                    {
                        var entry = ParseLogLine(line);
                        if (entry != null)
                            _logs.Add(entry);
                    }
                }
            }
            catch
            {
                // Ignore failures loading old logs
            }
        }

        private static LogEntry? ParseLogLine(string line)
        {
            try
            {
                // Format: [2025-12-08 03:00:00] [Info] Message
                if (line.Length < 25 || line[0] != '[')
                    return null;

                var tsEnd = line.IndexOf(']');
                if (tsEnd == -1) return null;

                var tsStr = line[1..tsEnd];
                if (!DateTime.TryParse(tsStr, out var ts))
                    return null;

                var levelStart = line.IndexOf('[', tsEnd + 1);
                var levelEnd = line.IndexOf(']', levelStart + 1);
                if (levelStart == -1 || levelEnd == -1) return null;

                var levelStr = line[(levelStart + 1)..levelEnd].Trim();
                if (!Enum.TryParse<LogLevel>(levelStr, true, out var level))
                    level = LogLevel.Info;

                var message = line[(levelEnd + 1)..].Trim();

                return new LogEntry
                {
                    Timestamp = ts,
                    Level = level,
                    Message = message
                };
            }
            catch
            {
                return null;
            }
        }

        public IEnumerable<LogEntry> GetLogs()
        {
            return _logs.OrderBy(l => l.Timestamp);
        }

        public string GetLogFilePath() => _logFilePath;

        public void ClearLogs()
        {
            try
            {
                // Clear in-memory logs
                _logs.Clear();

                // Clear file
                lock (_fileLock)
                {
                    File.WriteAllText(_logFilePath, string.Empty);
                }

                // Log that logs were cleared (new first entry)
                Log("Logs cleared by user", LogLevel.Info);
            }
            catch (Exception ex)
            {
                // If clearing fails, log error (best effort)
                Log($"Failed to clear logs: {ex.Message}", LogLevel.Error);
            }
        }

        public void ArchiveLogs()
        {
            try
            {
                lock (_fileLock)
                {
                    if (!File.Exists(_logFilePath))
                        return;

                    var archivePath = Path.Combine(
                        Path.GetDirectoryName(_logFilePath)!,
                        $"application_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                    File.Move(_logFilePath, archivePath);
                }

                Log("Logs archived", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Log($"Failed to archive logs: {ex.Message}", LogLevel.Error);
            }
        }

        public void PruneOldLogs(int daysToKeep = 30)
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-daysToKeep);

                // Rebuild file from recent logs
                lock (_fileLock)
                {
                    var recentLogs = _logs
                        .Where(l => l.Timestamp >= cutoff)
                        .OrderBy(l => l.Timestamp)
                        .ToList();

                    var lines = recentLogs.Select(entry =>
                        $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Message}");

                    File.WriteAllLines(_logFilePath, lines);
                }

                Log($"Pruned logs older than {daysToKeep} days", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Log($"Failed to prune old logs: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
