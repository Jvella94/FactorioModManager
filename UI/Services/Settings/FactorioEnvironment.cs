using FactorioModManager.Services.Infrastructure;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

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

        // Returns explicit data path if configured in settings (may be null)
        string? GetDataPath();

        void DetectEnvironment();
    }

    public partial class FactorioEnvironment(ISettingsService settingsService, ILogService logService) : IFactorioEnvironment
    {
        private readonly ISettingsService _settingsService = settingsService;
        private readonly ILogService _logService = logService;

        private static readonly Regex _versionRegex = VersionRegex();

        public string? GetExecutablePath() => _settingsService.GetFactorioExecutablePath();

        public void SetExecutablePath(string path) => _settingsService.SetFactorioExecutablePath(path);

        public string? GetVersion() => _settingsService.GetFactorioVersion();

        public void SetVersion(string? version) => _settingsService.SetFactorioVersion(version);

        public bool HasSpaceAgeDlc() => _settingsService.GetHasSpaceAgeDLC();

        public void SetHasSpaceAgeDlc(bool value) => _settingsService.SetHasSpaceAgeDlc(value);

        public string? GetDataPath()
        {
            return FolderPathHelper.GetFactorioDataPath(_logService, _settingsService.GetFactorioDataPath(), _settingsService.GetFactorioExecutablePath());
        }

        public void DetectEnvironment()
        {
            try
            {
                var exePath = GetExecutablePath();

                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = FolderPathHelper.DetectFactorioExecutable(_logService);
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

                // Prefer FileVersion (loads better on Windows), then ProductVersion
                string? rawVersion = !string.IsNullOrEmpty(fvi.FileVersion) ? fvi.FileVersion :
                                    !string.IsNullOrEmpty(fvi.ProductVersion) ? fvi.ProductVersion : null;

                string? version = null;

                if (!string.IsNullOrEmpty(rawVersion))
                {
                    var m = _versionRegex.Match(rawVersion);
                    version = m.Success ? m.Groups[1].Value : rawVersion.Trim();
                }

                // If still not found, try invoking the executable with --version and parse output
                if (string.IsNullOrEmpty(version))
                {
                    version = GetVersionFromRunningApp(exePath);
                }

                if (!string.IsNullOrEmpty(version))
                {
                    SetVersion(version);
                    _logService.Log($"Detected Factorio version: {version}");
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Failed to read Factorio version: {ex.Message}");
            }
        }

        private string GetVersionFromRunningApp(string exePath)
        {
            string versionResult = string.Empty;
            Process? proc = null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                proc = Process.Start(psi);
                if (proc != null)
                {
                    // Read output asynchronously with timeout to avoid blocking if process doesn't exit
                    var outputTask = proc.StandardOutput.ReadToEndAsync();
                    if (outputTask.Wait(1500))
                    {
                        var output = outputTask.Result ?? string.Empty;

                        // Prefer lines starting with 'Version:' (common in Factorio output)
                        var versionLineMatch = VersionRegex().Match(output);
                        if (versionLineMatch.Success)
                        {
                            versionResult = versionLineMatch.Groups[1].Value;
                        }
                        else
                        {
                            var m2 = _versionRegex.Match(output);
                            if (m2.Success)
                                versionResult = m2.Groups[1].Value;
                        }

                        // Ensure the process is not left running
                        try
                        {
                            if (!proc.HasExited)
                            {
                                proc.Kill(true);
                                proc.WaitForExit(1000);
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        // Timed out reading output -> ensure process is terminated
                        try
                        {
                            if (!proc.HasExited)
                            {
                                proc.Kill(true);
                                proc.WaitForExit(1000);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logService.LogWarning($"Failed to kill Factorio process after timeout: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Failed to execute Factorio for version detection: {ex.Message}");
            }
            finally
            {
                try
                {
                    proc?.Dispose();
                }
                catch { }
            }

            return versionResult;
        }

        private void DetectDLC(string exePath)
        {
            try
            {
                var dataDir = FolderPathHelper.GetFactorioDataPath(_logService, _settingsService.GetFactorioDataPath(), exePath);
                if (string.IsNullOrEmpty(dataDir) || !Directory.Exists(dataDir))
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

        [GeneratedRegex("^Version:\\s*(\\d+\\.\\d+(?:\\.\\d+)*)", RegexOptions.Multiline)]
        private static partial Regex VersionRegex();
    }
}