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
        private const int _maxMemoryLogs = 100;
        private bool _disposed;

        private bool _verboseLogging = false;

        public event EventHandler? LogsUpdated;

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
            LogInternal(message, level, null);
        }

        public void LogDebug(string message) => LogInternal(message, LogLevel.Debug, null);

        public void LogWarning(string message) => LogInternal(message, LogLevel.Warning, null);

        public void LogError(string message, Exception exception)
        {
            LogInternal(message, LogLevel.Error, null);
            LogDebug(_errorMessageService.GetTechnicalMessage(exception));
        }

        public void LogException(Exception exception)
        {
            LogInternal(_errorMessageService.GetTechnicalMessage(exception), LogLevel.Error, null);
        }

        // New structured/telemetry-style logging
        public void LogEvent(string eventName, IDictionary<string, object?>? properties = null, LogLevel level = LogLevel.Info)
        {
            var props = properties != null
                ? new Dictionary<string, object?>(properties)
                : [];

            props["event"] = eventName;
            LogInternal($"Event: {eventName}", level, props);
        }

        private void LogInternal(string message, LogLevel level, IDictionary<string, object?>? properties)
        {
            if (_disposed) return;

            // Respect global verbose setting: drop debug messages unless enabled
            if (level == LogLevel.Debug && !_verboseLogging)
                return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Message = message,
                Level = level
            };

            // If structured properties provided, include them in the Message as JSON suffix for file logs
            if (properties != null && properties.Count > 0)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(properties);
                    // Replace use of 'with' expression (LogEntry isn't a record) with creating a new LogEntry when adding JSON
                    entry = new LogEntry
                    {
                        Timestamp = entry.Timestamp,
                        Level = entry.Level,
                        Message = entry.Message + " | " + json
                    };
                }
                catch { }
            }

            _logQueue.Enqueue(entry);
            while (_logQueue.Count > _maxMemoryLogs)
            {
                _logQueue.TryDequeue(out _);
            }

            // Notify subscribers that logs changed
            try
            {
                LogsUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch { }

            try
            {
                lock (_fileLock)
                {
                    if (_disposed) return;

                    // Write base line
                    var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Message}";

                    // Append structured properties as JSON when present
                    if (properties != null && properties.Count > 0)
                    {
                        try
                        {
                            var json = System.Text.Json.JsonSerializer.Serialize(properties);
                            line += " | " + json;
                        }
                        catch { }
                    }

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

                // notify that logs were loaded
                try { LogsUpdated?.Invoke(this, EventArgs.Empty); } catch { }
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

                if (!Enum.TryParse<LogLevel>(levelStr, true, out var level))
                    level = LogLevel.Info;

                var message = line[(levelEnd + 1)..].Trim();

                // If message contains structured JSON (" | {..}"), strip it from Message field
                var pipeIndex = message.IndexOf('|');
                if (pipeIndex != -1)
                {
                    var msgPart = message.Substring(0, pipeIndex).Trim();
                    // leave JSON part out of in-memory Message to keep UI concise
                    message = msgPart;
                }

                return new LogEntry
                {
                    Timestamp = ts,
                    Message = message,
                    Level = level
                };
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

                try { LogsUpdated?.Invoke(this, EventArgs.Empty); } catch { }
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

                try { LogsUpdated?.Invoke(this, EventArgs.Empty); } catch { }

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

                try { LogsUpdated?.Invoke(this, EventArgs.Empty); } catch { }

                Log($"Pruned logs older than {daysToKeep} days");
            }
            catch (Exception ex)
            {
                LogError($"Failed to prune old logs: {ex.Message}", ex);
            }
        }

        // Global verbose controls
        public bool IsVerboseEnabled() => _verboseLogging;

        public void SetVerboseEnabled(bool enabled)
        {
            _verboseLogging = enabled;
            Log($"Verbose logging {(enabled ? "enabled" : "disabled")}");
        }
    }
}