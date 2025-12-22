using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Settings;
using static FactorioModManager.Constants;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FactorioModManager.Services.Mods
{
    public interface IModVersionManager
    {
        List<string> GetInstalledVersions(string modName);

        void DeleteVersion(string modName, string version);

        Task<Result<bool>> DownloadVersionAsync(
            string modName,
            string version,
            string downloadUrl,
            IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null,
            CancellationToken cancellationToken = default);

        void RefreshVersionCache(string modName);

        void ClearVersionCache();
    }

    public class ModVersionManager(
        ILogService logService,
        IModPathSettings pathSettings,
        IDownloadService downloadService) : IModVersionManager
    {
        private readonly ILogService _logService = logService;
        private readonly IModPathSettings _pathSettings = pathSettings;
        private readonly IDownloadService _downloadService = downloadService;
        private readonly Dictionary<string, List<string>> _versionCache = [];

        public List<string> GetInstalledVersions(string modName)
        {
            if (string.IsNullOrWhiteSpace(modName))
                return [];

            var name = DependencyHelper.ExtractDependencyName(modName);
            if (string.IsNullOrEmpty(name))
                name = modName;

            if (_versionCache.TryGetValue(name, out var cachedVersions))
            {
                return cachedVersions;
            }

            var modsDirectory = _pathSettings.GetModsPath();
            var versions = new List<string>();

            try
            {
                if (!Directory.Exists(modsDirectory))
                {
                    _logService.LogWarning($"Mods directory not found: {modsDirectory}");
                    _versionCache[name] = versions;
                    return versions;
                }

                // 1) Parse zip filenames: <mod>_<version>.zip
                var zipFiles = Directory.GetFiles(modsDirectory, $"{name}_*.zip");
                foreach (var zip in zipFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(zip);
                        var lastUnderscore = fileName.LastIndexOf('_');
                        if (lastUnderscore <= 0) continue;
                        var basePart = fileName[..lastUnderscore];
                        // Ensure the base part exactly matches the expected mod name to avoid false matches like "A_B_version" for mod "A"
                        if (!basePart.Equals(name, StringComparison.OrdinalIgnoreCase))
                            continue;
                        var version = fileName[(lastUnderscore + 1)..];
                        if (!string.IsNullOrEmpty(version))
                            versions.Add(version);
                    }
                    catch { /* ignore filename parse errors */ }
                }

                // 2) Inspect directories: read info.json inside directory to determine mod name + version
                var dirs = Directory.GetDirectories(modsDirectory);
                foreach (var dir in dirs)
                {
                    try
                    {
                        var infoPath = Path.Combine(dir, FileSystem.InfoJsonFileName);
                        if (!File.Exists(infoPath))
                            continue;

                        var json = File.ReadAllText(infoPath);
                        var modInfo = JsonSerializer.Deserialize<ModInfo>(json, JsonOptions.CaseInsensitive);
                        if (modInfo != null && string.Equals(modInfo.Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(modInfo.Version))
                                versions.Add(modInfo.Version);
                        }
                    }
                    catch { /* ignore malformed info.json */ }
                }

                // Deduplicate and sort using semantic-aware comparer when possible
                versions = [.. versions.Distinct(StringComparer.OrdinalIgnoreCase)];
                versions = [.. versions.OrderByDescending(v => v, Comparer<string>.Create((a, b) => VersionHelper.CompareVersions(a, b)))];
                _versionCache[name] = versions;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error getting installed versions for {name}: {ex.Message}", ex);
            }

            return versions;
        }

        public void DeleteVersion(string modName, string version)
        {
            var modsDirectory = _pathSettings.GetModsPath();
            var zipFileName = $"{modName}_{version}.zip";
            var zipFilePath = Path.Combine(modsDirectory, zipFileName);

            try
            {
                // Prefer the zip file if present
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                    _logService.Log($"Deleted {zipFileName}");
                    RefreshVersionCache(modName);
                    return;
                }

                // Try conventional directory name first
                var dirName = $"{modName}_{version}";
                var dirPath = Path.Combine(modsDirectory, dirName);
                if (Directory.Exists(dirPath))
                {
                    Directory.Delete(dirPath, recursive: true);
                    _logService.Log($"Deleted directory {dirName}");
                    RefreshVersionCache(modName);
                    return;
                }

                // If no conventional directory, scan directories and inspect info.json to find the matching version
                var dirs = Directory.GetDirectories(modsDirectory);
                foreach (var d in dirs)
                {
                    try
                    {
                        var infoPath = Path.Combine(d, FileSystem.InfoJsonFileName);
                        if (!File.Exists(infoPath))
                            continue;

                        var json = File.ReadAllText(infoPath);
                        var modInfo = JsonSerializer.Deserialize<ModInfo>(json, JsonOptions.CaseInsensitive);
                        if (modInfo != null
                            && string.Equals(modInfo.Name, modName, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(modInfo.Version, version, StringComparison.OrdinalIgnoreCase))
                        {
                            Directory.Delete(d, recursive: true);
                            _logService.Log($"Deleted directory {Path.GetFileName(d)}");
                            RefreshVersionCache(modName);
                            return;
                        }
                    }
                    catch { /* ignore per-directory failures */ }
                }

                _logService.LogWarning($"Version file or directory not found: {zipFileName} or any directory with {FileSystem.InfoJsonFileName} matching {modName}@{version}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error deleting version {version} of {modName}: {ex.Message}", ex);
                throw;
            }
        }

        public async Task<Result<bool>> DownloadVersionAsync(
            string modName,
            string version,
            string downloadUrl,
            IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var modsDirectory = _pathSettings.GetModsPath();
            var fileName = $"{modName}_{version}.zip";
            var filePath = Path.Combine(modsDirectory, fileName);

            try
            {
                _logService.Log($"Downloading {fileName}");
                var result = await _downloadService.DownloadFileAsync(
                    downloadUrl,
                    filePath,
                    progress,
                    cancellationToken);

                if (result.Success)
                {
                    _logService.Log($"Successfully downloaded {fileName}");
                    RefreshVersionCache(modName);
                }
                else
                {
                    _logService.LogWarning($"Failed to download {fileName}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error downloading {modName} version {version}: {ex.Message}", ex);

                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { }
                }

                return Result.Fail<bool>(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        public void RefreshVersionCache(string modName)
        {
            if (string.IsNullOrEmpty(modName)) return;
            _versionCache.Remove(modName);
        }

        public void ClearVersionCache()
        {
            _versionCache.Clear();
            _logService.Log("Version cache cleared");
        }
    }
}