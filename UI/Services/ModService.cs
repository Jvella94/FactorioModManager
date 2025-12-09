using Avalonia.Media.Imaging;
using FactorioModManager.Models;
using FactorioModManager.Models.API;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FactorioModManager.Services
{
    public class ModService(
        ILogService logService,
        ISettingsService settingsService,
        IDownloadService downloadService) : IModService
    {
        private readonly ILogService _logService = logService;
        private readonly ISettingsService _settingsService = settingsService;
        private readonly IDownloadService _downloadService = downloadService;

        // ✅ Version cache
        private readonly Dictionary<string, List<string>> _versionCache = [];

        // ✅ Thumbnail cache with weak references (allows GC when memory is low)
        private readonly Dictionary<string, WeakReference<Bitmap>> _thumbnailCache = [];

        // ✅ Thumbnail cache statistics
        private int _thumbnailCacheHits = 0;

        private int _thumbnailCacheMisses = 0;

        public string GetModsDirectory()
        {
            var customPath = _settingsService.GetModsPath();
            return !string.IsNullOrEmpty(customPath)
                ? customPath
                : ModPathHelper.GetModsDirectory();
        }

        public List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> LoadAllMods()
        {
            var modsDirectory = GetModsDirectory();
            var modListPath = Path.Combine(modsDirectory, Constants.FileSystem.ModListFileName);

            _logService.Log($"Loading mods from: {modsDirectory}");

            if (!Directory.Exists(modsDirectory))
            {
                _logService.LogWarning($"Mods directory not found: {modsDirectory}");
                return [];
            }

            var enabledStates = LoadModListJson(modListPath);
            var mods = new List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)>();

            // Load from ZIP files
            var zipFiles = Directory.GetFiles(modsDirectory, "*.zip");
            foreach (var zipFile in zipFiles)
            {
                try
                {
                    var modInfo = ExtractModInfoFromZip(zipFile);
                    if (modInfo != null)
                    {
                        var isEnabled = !enabledStates.TryGetValue(modInfo.Name, out var enabled) || enabled;
                        var lastUpdated = File.GetLastWriteTime(zipFile);
                        var thumbnailPath = FindThumbnailInZip(zipFile);

                        mods.Add((modInfo, isEnabled, lastUpdated, thumbnailPath, zipFile));
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error loading mod from {zipFile}: {ex.Message}", ex);
                }
            }

            // Load from directories
            var directories = Directory.GetDirectories(modsDirectory)
                .Where(d => !d.EndsWith(Constants.FileSystem.ModSettingsFolder));

            foreach (var dir in directories)
            {
                try
                {
                    var infoPath = Path.Combine(dir, Constants.FileSystem.InfoJsonFileName);
                    if (File.Exists(infoPath))
                    {
                        var modInfo = LoadModInfoFromJson(infoPath);
                        if (modInfo != null)
                        {
                            var isEnabled = !enabledStates.TryGetValue(modInfo.Name, out var enabled) || enabled;
                            var lastUpdated = Directory.GetLastWriteTime(dir);
                            var thumbnailPath = FindThumbnailInDirectory(dir);

                            mods.Add((modInfo, isEnabled, lastUpdated, thumbnailPath, dir));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error loading mod from {dir}: {ex.Message}", ex);
                }
            }

            _logService.Log($"Loaded {mods.Count} total mods including old versions.");
            return mods;
        }

        public void ToggleMod(string modName, bool isEnabled)
        {
            var modsDirectory = GetModsDirectory();
            var modListPath = Path.Combine(modsDirectory, Constants.FileSystem.ModListFileName);

            try
            {
                var modList = LoadModListJson(modListPath);
                modList[modName] = isEnabled;
                SaveModListJson(modListPath, modList);

                _logService.Log($"Toggled {modName}: {(isEnabled ? "enabled" : "disabled")}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error toggling mod {modName}: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// ✅ Loads a thumbnail with caching
        /// </summary>
        public async Task<Bitmap?> LoadThumbnailAsync(string thumbnailPath)
        {
            if (string.IsNullOrEmpty(thumbnailPath))
                return null;

            // ✅ Check cache first
            if (_thumbnailCache.TryGetValue(thumbnailPath, out var weakRef))
            {
                if (weakRef.TryGetTarget(out var cachedThumbnail))
                {
                    _thumbnailCacheHits++;
                    _logService.LogDebug($"Thumbnail cache hit: {thumbnailPath} (hits: {_thumbnailCacheHits}, misses: {_thumbnailCacheMisses})");
                    return cachedThumbnail;
                }
                else
                {
                    // Weak reference was collected, remove from cache
                    _thumbnailCache.Remove(thumbnailPath);
                }
            }

            _thumbnailCacheMisses++;

            return await Task.Run(() =>
            {
                try
                {
                    Bitmap? thumbnail = null;

                    // Load from zip or file
                    if (thumbnailPath.Contains('|'))
                    {
                        var parts = thumbnailPath.Split('|');
                        if (parts.Length == 2)
                        {
                            thumbnail = LoadThumbnailFromZip(parts[0], parts[1]);
                        }
                    }
                    else if (File.Exists(thumbnailPath))
                    {
                        thumbnail = new Bitmap(thumbnailPath);
                    }

                    // ✅ Cache the loaded thumbnail with weak reference
                    if (thumbnail != null)
                    {
                        _thumbnailCache[thumbnailPath] = new WeakReference<Bitmap>(thumbnail);
                        _logService.LogDebug($"Cached thumbnail: {thumbnailPath} (cache size: {_thumbnailCache.Count})");
                    }

                    return thumbnail;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error loading thumbnail from {thumbnailPath}: {ex.Message}", ex);
                    return null;
                }
            });
        }

        /// <summary>
        /// ✅ Clears the thumbnail cache
        /// </summary>
        public void ClearThumbnailCache()
        {
            _thumbnailCache.Clear();
            _thumbnailCacheHits = 0;
            _thumbnailCacheMisses = 0;
            _logService.Log("Thumbnail cache cleared");
        }

        /// <summary>
        /// ✅ Gets cache statistics
        /// </summary>
        public (int CacheSize, int Hits, int Misses, double HitRate) GetThumbnailCacheStats()
        {
            var total = _thumbnailCacheHits + _thumbnailCacheMisses;
            var hitRate = total > 0 ? (_thumbnailCacheHits / (double)total) * 100 : 0;
            return (_thumbnailCache.Count, _thumbnailCacheHits, _thumbnailCacheMisses, hitRate);
        }

        public List<string> GetInstalledVersions(string modName)
        {
            // ✅ Check cache first
            if (_versionCache.TryGetValue(modName, out var cachedVersions))
            {
                return cachedVersions;
            }

            var modsDirectory = GetModsDirectory();
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

                // ✅ Cache the result
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
            var modsDirectory = GetModsDirectory();
            var fileName = $"{modName}_{version}.zip";
            var filePath = Path.Combine(modsDirectory, fileName);

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logService.Log($"Deleted {fileName}");

                    // ✅ Clear cache for this mod
                    _versionCache.Remove(modName);
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

        public void RefreshInstalledVersions(string modName)
        {
            _versionCache.Remove(modName);
            GetInstalledVersions(modName);
        }

        public async Task DownloadVersionAsync(
            string modName,
            string version,
            string downloadUrl,
            IProgress<(long bytesDownloaded, long? totalBytes)> progress, CancellationToken cancellationToken = default)
        {
            var modsDirectory = GetModsDirectory();
            var fileName = $"{modName}_{version}.zip";
            var filePath = Path.Combine(modsDirectory, fileName);
            try
            {
                _logService.Log($"Downloading {fileName} from {downloadUrl}");
                var result = await _downloadService.DownloadFileAsync(downloadUrl, filePath, progress, cancellationToken);
                if (result == null || result.Success == false)
                {
                    _logService.LogWarning($"Did not manage to download {fileName}");
                    if (result != null) _logService.LogWarning($"Code: {result.Code} Error:{result.Error}");
                    return;
                }
                _logService.Log($"Successfully downloaded {fileName}");
                // ✅ Clear version cache
                _versionCache.Remove(modName);
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

                throw;
            }
        }

        #region Private Helper Methods

        private Dictionary<string, bool> LoadModListJson(string modListPath)
        {
            var enabledStates = new Dictionary<string, bool>();

            if (!File.Exists(modListPath))
            {
                _logService.LogWarning("mod-list.json not found, all mods will be enabled by default");
                return enabledStates;
            }

            try
            {
                var json = File.ReadAllText(modListPath);
                var modList = JsonSerializer.Deserialize<ModList>(json);

                if (modList?.Mods != null)
                {
                    foreach (var entry in modList.Mods)
                    {
                        enabledStates[entry.Name] = entry.Enabled;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error reading mod-list.json: {ex.Message}", ex);
            }

            return enabledStates;
        }

        private void SaveModListJson(string modListPath, Dictionary<string, bool> enabledStates)
        {
            try
            {
                var modList = new ModList
                {
                    Mods = [.. enabledStates.Select(kvp => new ModListEntry
                    {
                        Name = kvp.Key,
                        Enabled = kvp.Value
                    })]
                };

                var json = JsonSerializer.Serialize(modList, Constants.JsonHelper.CamelCase);
                File.WriteAllText(modListPath, json);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error writing mod-list.json: {ex.Message}", ex);
                throw;
            }
        }

        private ModInfo? ExtractModInfoFromZip(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var infoEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith(Constants.FileSystem.InfoJsonFileName, StringComparison.OrdinalIgnoreCase));

                if (infoEntry != null)
                {
                    using var stream = infoEntry.Open();
                    using var reader = new StreamReader(stream);
                    var json = reader.ReadToEnd();
                    return JsonSerializer.Deserialize<ModInfo>(json);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error extracting info from {zipPath}: {ex.Message}", ex);
            }

            return null;
        }

        private ModInfo? LoadModInfoFromJson(string infoPath)
        {
            try
            {
                var json = File.ReadAllText(infoPath);
                return JsonSerializer.Deserialize<ModInfo>(json);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error loading info.json from {infoPath}: {ex.Message}", ex);
                return null;
            }
        }

        private string? FindThumbnailInZip(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var thumbnailEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith("thumbnail.png", StringComparison.OrdinalIgnoreCase));

                if (thumbnailEntry != null)
                {
                    return $"{zipPath}|{thumbnailEntry.FullName}";
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error finding thumbnail in {zipPath}: {ex.Message}", ex);
            }

            return null;
        }

        private static string? FindThumbnailInDirectory(string directory)
        {
            var thumbnailPath = Path.Combine(directory, "thumbnail.png");
            return File.Exists(thumbnailPath) ? thumbnailPath : null;
        }

        private Bitmap? LoadThumbnailFromZip(string zipPath, string entryPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var entry = archive.GetEntry(entryPath);

                if (entry != null)
                {
                    using var stream = entry.Open();
                    using var memStream = new MemoryStream();
                    stream.CopyTo(memStream);
                    memStream.Position = 0;
                    return new Bitmap(memStream);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error loading thumbnail from zip {zipPath}: {ex.Message}", ex);
            }

            return null;
        }

        #endregion Private Helper Methods
    }
}