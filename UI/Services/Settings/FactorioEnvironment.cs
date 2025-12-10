using FactorioModManager.Services.Infrastructure;
using System;
using System.Diagnostics;
using System.IO;

namespace FactorioModManager.Services.Settings
{
    public interface IFactorioEnvironment
    {
        string? GetExecutablePath();

        void SetExecutablePath(string path);

        string? GetVersion();

        void SetVersion(string? version);

        bool HasSpaceAgeDlc();

        void SetHasSpaceAgeDlc(bool value);

        void DetectEnvironment();
    }

    public class FactorioEnvironment(ISettingsService settingsService, ILogService logService) : IFactorioEnvironment
    {
        private readonly ISettingsService _settingsService = settingsService;
        private readonly ILogService _logService = logService;

        public string? GetExecutablePath() => _settingsService.GetFactorioExecutablePath();

        public void SetExecutablePath(string path) => _settingsService.SetFactorioExecutablePath(path);

        public string? GetVersion() => _settingsService.GetFactorioVersion();

        public void SetVersion(string? version) => _settingsService.SetFactorioVersion(version);

        public bool HasSpaceAgeDlc() => _settingsService.GetHasSpaceAgeDLC();

        public void SetHasSpaceAgeDlc(bool value) => _settingsService.SetHasSpaceAgeDlc(value);

        public void DetectEnvironment()
        {
            try
            {
                var exePath = GetExecutablePath();
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = FolderPathHelper.DetectFactorioExecutable();
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        SetExecutablePath(exePath);
                        _logService.Log($"Auto-detected Factorio at: {exePath}");
                    }
                    else
                    {
                        return;
                    }
                }

                if (!File.Exists(exePath))
                    return;

                // Detect version
                DetectVersion(exePath);

                // Detect DLC
                DetectDLC(exePath);
            }
            catch (Exception ex)
            {
                _logService.LogError("Error detecting Factorio environment", ex);
            }
        }

        private void DetectVersion(string exePath)
        {
            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrEmpty(fvi.FileVersion))
                {
                    SetVersion(fvi.FileVersion);
                    _logService.Log($"Detected Factorio version: {fvi.FileVersion}");
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Failed to read Factorio version: {ex.Message}");
            }
        }

        private void DetectDLC(string exePath)
        {
            try
            {
                var rootDir = Path.GetDirectoryName(exePath);
                if (string.IsNullOrEmpty(rootDir))
                    return;

                var dataDir = Path.Combine(rootDir, "data");
                if (!Directory.Exists(dataDir))
                    return;

                bool hasSpaceAgeDlc =
                    Directory.Exists(Path.Combine(dataDir, "space-age")) ||
                    Directory.Exists(Path.Combine(dataDir, "quality")) ||
                    Directory.Exists(Path.Combine(dataDir, "elevated-rails"));

                SetHasSpaceAgeDlc(hasSpaceAgeDlc);
                _logService.Log($"Detected Space Age DLC: {hasSpaceAgeDlc}");
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Failed to detect DLC: {ex.Message}");
            }
        }
    }
}