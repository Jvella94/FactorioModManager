using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace FactorioModManager.Services
{
    public class LogService
    {
        private static LogService? _instance;
        public static LogService Instance => _instance ??= new LogService();

        public ObservableCollection<string> Logs { get; } = [];

        private LogService()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
            var os = RuntimeInformation.OSDescription;
            var utcTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

            Log($"=== Factorio Mod Manager v{version} ===");
            Log($"OS: {os}");
            Log($"Started at: {utcTime}");
            Log("=====================================");
        }

        public void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}";

            Debug.WriteLine(logEntry);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Logs.Add(logEntry);

                while (Logs.Count > 1000)
                {
                    Logs.RemoveAt(0);
                }
            });
        }

        // ADDED: Helper to log and debug in one call
        public static void LogDebug(string message)
        {
            Instance.Log(message);
        }

        public string GetAllLogs()
        {
            return string.Join(Environment.NewLine, Logs);
        }
    }
}
