using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            if (_versionCache.TryGetValue(modName, out var cachedVersions))
            {
                return cachedVersions;
            }

            var modsDirectory = _pathSettings.GetModsPath();
            var versions = new List<string>();

            try
            {
                var modFiles = Directory.GetFiles(modsDirectory, $"{modName}_*.zip");

                foreach (var file in modFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var parts = fileName.Split('_');

                    if (parts.Length >= 2)
                    {
                        var version = parts[^1];
                        versions.Add(version);
                    }
                }

                versions = [.. versions.OrderByDescending(v => v)];
                _versionCache[modName] = versions;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error getting installed versions for {modName}: {ex.Message}", ex);
            }

            return versions;
        }

        public void DeleteVersion(string modName, string version)
        {
            var modsDirectory = _pathSettings.GetModsPath();
            var fileName = $"{modName}_{version}.zip";
            var filePath = Path.Combine(modsDirectory, fileName);

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logService.Log($"Deleted {fileName}");
                    RefreshVersionCache(modName);
                }
                else
                {
                    _logService.LogWarning($"Version file not found: {fileName}");
                }
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
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch { }
                }

                return Result.Fail<bool>(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        public void RefreshVersionCache(string modName)
        {
            _versionCache.Remove(modName);
        }

        public void ClearVersionCache()
        {
            _versionCache.Clear();
            _logService.Log("Version cache cleared");
        }
    }
}