using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using FluentAssertions;
using Xunit;

namespace FactorioModManager.Tests.Services
{
    public class LogServiceTests : IDisposable
    {
        private readonly string _originalAppData;
        private readonly LogService _logService;

        public LogServiceTests()
        {
            _originalAppData = Environment.GetEnvironmentVariable("APPDATA") ?? "";
            Environment.SetEnvironmentVariable("APPDATA", Path.Combine(Path.GetTempPath(), "FMM-Test"));

            _logService = new LogService();
        }

        [Fact]
        public void Log_WritesToMemoryAndFile()
        {
            _logService.ClearLogs();
            // Act
            _logService.Log("Test message", LogLevel.Info);

            // Assert
            var logs = _logService.GetLogs().ToList();
            logs.Should().HaveCount(1);
            logs[0].Message.Should().Be("Test message");
            logs[0].Level.Should().Be(LogLevel.Info);

            var logFile = _logService.GetLogFilePath();
            File.Exists(logFile).Should().BeTrue();
            File.ReadAllText(logFile).Should().Contain("Test message");
        }

        [Fact]
        public void ClearLogs_RemovesAllLogs()
        {
            // Arrange
            _logService.Log("Before clear");
            _logService.Log("Before clear 2");

            // Act
            _logService.ClearLogs();

            // Assert
            var logs = _logService.GetLogs().ToList();
            logs.Should().HaveCount(0);

            var logFile = _logService.GetLogFilePath();
            File.ReadAllLines(logFile).Should().HaveCount(0);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("APPDATA", _originalAppData);
            try
            {
                var logPath = Path.Combine(Path.GetTempPath(), "FMM-Test", "FactorioModManager", "application.log");
                if (File.Exists(logPath))
                    File.Delete(logPath);
                GC.SuppressFinalize(this);
            }
            catch { }
        }
    }
}