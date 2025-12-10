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
        private readonly string _logFilePath;
        private readonly Lock _fileLock = new();
        private readonly ConcurrentQueue<LogEntry> _logQueue = new();
        private StreamWriter _logWriter;
        private readonly Timer _flushTimer;
        private readonly IErrorMessageService _errorMessageService;
        private const int _maxMemoryLogs = 1000;
        private bool _disposed;

        public LogService(IErrorMessageService errorMessageService)
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FactorioModManager");
            Directory.CreateDirectory(appDataPath);

            _logFilePath = Path.Combine(appDataPath, "application.log");

            _logWriter = new StreamWriter(
                path: _logFilePath,
                append: true,
                encoding: System.Text.Encoding.UTF8,
                bufferSize: 4096)
            {
                AutoFlush = false
            };

            // Flush every 2 seconds
            _flushTimer = new Timer(_ => FlushLogs(), null,
                TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

            LoadLogsFromFile();
            _errorMessageService = errorMessageService;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _flushTimer?.Dispose();

            lock (_fileLock)
            {
                _logWriter?.Flush();
                _logWriter?.Dispose();
            }
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            LogInternal(message, level);
        }

        public void LogDebug(string message) => LogInternal(message, LogLevel.Debug);

        public void LogWarning(string message) => LogInternal(message, LogLevel.Warning);

        public void LogError(string message, Exception exception)
        {
            LogInternal(message, LogLevel.Error);
            LogDebug(_errorMessageService.GetTechnicalMessage(exception));
        }

        private void LogInternal(string message, LogLevel level)
        {
            if (_disposed) return;

            var entry = new LogEntry(DateTime.UtcNow, message, level);

            _logQueue.Enqueue(entry);
            while (_logQueue.Count > _maxMemoryLogs)
            {
                _logQueue.TryDequeue(out _);
            }

            try
            {
                lock (_fileLock)
                {
                    if (_disposed) return;
                    var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Message}";
                    _logWriter?.WriteLine(line);
                }
            }
            catch (Exception ex) { LogError($"Issue Logging Internally: {ex.Message}", ex); }
        }

        private void FlushLogs()
        {
            try
            {
                lock (_fileLock)
                {
                    _logWriter?.Flush();
                }
            }
            catch (Exception ex) { LogError($"Issue Flushing Logs : {ex.Message}", ex); }
        }

        private void LoadLogsFromFile()
        {
            try
            {
                if (!File.Exists(_logFilePath))
                    return;

                using var fileStream = new FileStream(
                  _logFilePath,
                  FileMode.Open,
                  FileAccess.Read,
                  FileShare.ReadWrite); // Allow other processes/threads to write

                using var reader = new StreamReader(fileStream);

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var entry = ParseLogLine(line);
                    if (entry != null)
                    {
                        _logQueue.Enqueue(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Could not load logs from log file {_logFilePath}", ex);
            }
        }

        private LogEntry? ParseLogLine(string line)
        {
            try
            {
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

                // ✅ Add <LogLevel> generic type parameter
                if (!Enum.TryParse<LogLevel>(levelStr, true, out var level))
                    level = LogLevel.Info;

                var message = line[(levelEnd + 1)..].Trim();

                return new LogEntry(ts, message, level);
            }
            catch (Exception ex)
            {
                LogError($"Failed to parse log lines {ex.Message}", ex);
                return null;
            }
        }

        public IEnumerable<LogEntry> GetLogs()
        {
            // ✅ Return snapshot from _logQueue instead of _logs
            return _logQueue.ToList().OrderBy(l => l.Timestamp);
        }

        public string GetLogFilePath() => _logFilePath;

        public void ClearLogs()
        {
            try
            {
                while (_logQueue.TryDequeue(out _)) { }

                lock (_fileLock)
                {
                    // ✅ Close writer, clear file, reopen writer
                    _logWriter?.Flush();
                    _logWriter?.Dispose();

                    File.WriteAllText(_logFilePath, string.Empty);

                    _logWriter = new StreamWriter(
                        path: _logFilePath,
                        append: true,
                        encoding: System.Text.Encoding.UTF8,
                        bufferSize: 4096)
                    {
                        AutoFlush = false
                    };
                }
            }
            catch (Exception ex)
            {
                // Log will recreate writer if needed
                LogError($"Failed to clear logs: {ex.Message}", ex);
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

                    // ✅ Close the writer first
                    _logWriter?.Flush();
                    _logWriter?.Dispose();

                    var archivePath = Path.Combine(
                        Path.GetDirectoryName(_logFilePath)!,
                        $"application_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");

                    File.Move(_logFilePath, archivePath);

                    // ✅ Create new empty file and reopen writer
                    File.WriteAllText(_logFilePath, string.Empty);

                    _logWriter = new StreamWriter(
                        path: _logFilePath,
                        append: true,
                        encoding: System.Text.Encoding.UTF8,
                        bufferSize: 4096)
                    {
                        AutoFlush = false
                    };
                }

                Log("Logs archived");
            }
            catch (Exception ex)
            {
                LogError($"Failed to archive logs: {ex.Message}", ex);
            }
        }

        public void PruneOldLogs(int daysToKeep = 30)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);

                var recentLogs = _logQueue
                    .Where(l => l.Timestamp >= cutoff)
                    .OrderBy(l => l.Timestamp)
                    .ToList();

                lock (_fileLock)
                {
                    // ✅ Close the writer first
                    _logWriter?.Flush();
                    _logWriter?.Dispose();

                    // Rewrite file with only recent logs
                    var lines = recentLogs.Select(entry =>
                        $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Message}");

                    File.WriteAllLines(_logFilePath, lines);

                    // ✅ Reopen the writer
                    _logWriter = new StreamWriter(
                        path: _logFilePath,
                        append: true,
                        encoding: System.Text.Encoding.UTF8,
                        bufferSize: 4096)
                    {
                        AutoFlush = false
                    };
                }

                Log($"Pruned logs older than {daysToKeep} days");
            }
            catch (Exception ex)
            {
                LogError($"Failed to prune old logs: {ex.Message}", ex);
            }
        }

        public void LogException(Exception exception)
        {
            LogDebug(_errorMessageService.GetTechnicalMessage(exception));
        }
    }
}