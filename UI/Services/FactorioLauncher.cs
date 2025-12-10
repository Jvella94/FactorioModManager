using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Settings;
using System;
using System.Diagnostics;
using System.IO;

namespace FactorioModManager.Services
{
    public interface IFactorioLauncher
    {
        Result Launch();
        string? DetectFactorioPath();
        bool IsFactorioInstalled();
    }

    public class FactorioLauncher(IFactorioEnvironment environment, ILogService logService) : IFactorioLauncher
    {
        private readonly IFactorioEnvironment _environment = environment;
        private readonly ILogService _logService = logService;

        public Result Launch()
        {
            try
            {
                var factorioPath = _environment.GetExecutablePath();

                // If path is not configured, try to auto-detect it
                if (string.IsNullOrEmpty(factorioPath))
                {
                    factorioPath = DetectFactorioPath();

                    if (string.IsNullOrEmpty(factorioPath))
                    {
                        var msg = "Factorio path not configured and could not be auto-detected. Please set it in Settings.";
                        _logService.LogWarning(msg);
                        return Result.Fail(msg, ErrorCode.FileNotFound);
                    }

                    // Save the detected path for future use
                    _environment.SetExecutablePath(factorioPath);
                    _logService.Log($"Auto-detected and saved Factorio path: {factorioPath}");
                }

                if (!File.Exists(factorioPath))
                {
                    var msg = $"Factorio executable not found at: {factorioPath}";
                    _logService.LogError(msg, new FileNotFoundException("Factorio not found", factorioPath));
                    return Result.Fail(msg, ErrorCode.FileNotFound);
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = factorioPath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(factorioPath) ?? string.Empty
                };

                Process.Start(processInfo);
                _logService.Log($"Launched Factorio: {factorioPath}");

                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logService.LogError("Error launching Factorio", ex);
                return Result.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        public string? DetectFactorioPath()
        {
            return FolderPathHelper.DetectFactorioExecutable();
        }

        public bool IsFactorioInstalled()
        {
            var path = _environment.GetExecutablePath();
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }
    }
}