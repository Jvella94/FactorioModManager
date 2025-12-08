using FactorioModManager.Models;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FactorioModManager.Services
{
    public class ModService(
        ISettingsService settingsService,
        ILogService logService,
        IFactorioApiService apiService) : IModService
    {
        private readonly ISettingsService _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        private readonly ILogService _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        private readonly IFactorioApiService _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        private readonly Dictionary<string, HashSet<string>> _installedVersions = [];

        public string GetModsDirectory()
        {
            return _settingsService.GetModsPath();
        }

        public List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> LoadAllMods()
        {
            var modsDirectory = _settingsService.GetModsPath();
            var modListPath = Path.Combine(modsDirectory, Constants.FileSystem.ModListFileName);
            var enabledMods = new Dictionary<string, bool>();

            if (File.Exists(modListPath))
            {
                var jsonString = File.ReadAllText(modListPath);
                var modListData = JsonSerializer.Deserialize<ModList>(jsonString);
                if (modListData?.Mods != null)
                {
                    enabledMods = modListData.Mods.ToDictionary(m => m.Name, m => m.Enabled);
                }
            }

            var mods = new List<(ModInfo, bool, DateTime?, string?, string)>();

            // 1) ZIP mods
            var modFiles = Directory.GetFiles(modsDirectory, Constants.FileSystem.ModFilePattern);
            foreach (var modFile in modFiles)
            {
                try
                {
                    using var archive = ZipFile.OpenRead(modFile);
                    var infoEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.EndsWith(Constants.FileSystem.InfoJsonFileName, StringComparison.OrdinalIgnoreCase));

                    if (infoEntry != null)
                    {
                        using var stream = infoEntry.Open();
                        using var reader = new StreamReader(stream);
                        var json = reader.ReadToEnd();
                        var modInfo = JsonSerializer.Deserialize<ModInfo>(json);

                        if (modInfo != null)
                        {
                            var isEnabled = enabledMods.GetValueOrDefault(modInfo.Name, true);
                            var lastModified = File.GetLastWriteTime(modFile);
                            var thumbnailPath = FindThumbnail(archive, modFile);
                            mods.Add((modInfo, isEnabled, lastModified, thumbnailPath, modFile));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logService.Log($"Error loading mod {Path.GetFileName(modFile)}: {ex.Message}",
                        LogLevel.Error);
                }
            }

            // 2) Folder mods
            foreach (var dir in Directory.GetDirectories(modsDirectory))
            {
                var infoPath = Path.Combine(dir, Constants.FileSystem.InfoJsonFileName);
                if (!File.Exists(infoPath))
                    continue;

                try
                {
                    var json = File.ReadAllText(infoPath);
                    var modInfo = JsonSerializer.Deserialize<ModInfo>(json);
                    if (modInfo == null)
                        continue;

                    var isEnabled = enabledMods.GetValueOrDefault(modInfo.Name, true);
                    var lastModified = Directory.GetLastWriteTime(dir);

                    string? thumbnailPath = null;
                    var thumbOnDisk = Path.Combine(dir, "thumbnail.png");
                    if (File.Exists(thumbOnDisk))
                        thumbnailPath = thumbOnDisk;

                    mods.Add((modInfo, isEnabled, lastModified, thumbnailPath, dir));
                }
                catch (Exception ex)
                {
                    _logService.Log($"Error loading folder mod {Path.GetFileName(dir)}: {ex.Message}", LogLevel.Error);
                }
            }

            return mods;
        }

        private static string? FindThumbnail(ZipArchive archive, string modFile)
        {
            var thumbnailEntry = archive.Entries.FirstOrDefault(e =>
                e.FullName.EndsWith("thumbnail.png", StringComparison.OrdinalIgnoreCase));

            if (thumbnailEntry != null)
            {
                return $"{modFile}|{thumbnailEntry.FullName}";
            }

            return null;
        }

        public void ToggleMod(string modName, bool enabled)
        {
            var modsDirectory = _settingsService.GetModsPath();
            var modListPath = Path.Combine(modsDirectory, Constants.FileSystem.ModListFileName);
            ModList modListData;

            if (File.Exists(modListPath))
            {
                var jsonString = File.ReadAllText(modListPath);
                modListData = JsonSerializer.Deserialize<ModList>(jsonString) ?? new ModList();
            }
            else
            {
                modListData = new ModList();
            }

            var existingMod = modListData.Mods.FirstOrDefault(m => m.Name == modName);
            if (existingMod != null)
            {
                existingMod.Enabled = enabled;
            }
            else
            {
                modListData.Mods.Add(new ModListEntry { Name = modName, Enabled = enabled });
            }

            var updatedJson = JsonSerializer.Serialize(modListData, JsonOptions);
            File.WriteAllText(modListPath, updatedJson);
            _logService.Log($"Mod '{modName}' {(enabled ? "enabled" : "disabled")}");
        }

        public void RemoveMod(string modName)
        {
            var modsDirectory = _settingsService.GetModsPath();
            var modFiles = Directory.GetFiles(modsDirectory, $"{modName}_*.zip");

            foreach (var file in modFiles)
            {
                File.Delete(file);
                _logService.Log($"Removed mod file: {Path.GetFileName(file)}");
            }
        }

        public async Task DownloadVersionAsync(
            string modName,
            string version,
            string downloadUrl,
            IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var modsDir = _settingsService.GetModsPath();
            var zipPath = Path.Combine(modsDir, $"{modName}_{version}.zip");

            try
            {
                // Use API service to download
                await _apiService.DownloadModAsync(downloadUrl, zipPath, progress, cancellationToken);

                // Update installed versions cache
                RefreshInstalledVersions(modName);

                _logService.Log($"Successfully downloaded {modName} v{version}");
            }
            catch (OperationCanceledException)
            {
                _logService.LogWarning($"Download cancelled: {modName} v{version}");
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to download {modName} v{version}: {ex.Message}", ex);
                throw;
            }
        }

        public void DeleteVersion(string modName, string version)
        {
            var modsDir = _settingsService.GetModsPath();
            var filePath = Path.Combine(modsDir, $"{modName}_{version}.zip");

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                RefreshInstalledVersions(modName);
                _logService.Log($"Deleted {modName}_{version}.zip");
            }
        }

        public HashSet<string> GetInstalledVersions(string modName)
        {
            if (!_installedVersions.ContainsKey(modName))
            {
                RefreshInstalledVersions(modName);
            }

            return _installedVersions.GetValueOrDefault(modName, []);
        }

        public void RefreshInstalledVersions(string modName)
        {
            var modsDir = _settingsService.GetModsPath();
            var modPattern = $"{modName}_*.zip";
            var modFiles = Directory.GetFiles(modsDir, modPattern);
            var versions = new HashSet<string>();

            foreach (var file in modFiles)
            {
                var version = Path.GetFileNameWithoutExtension(file).Replace($"{modName}_", "");
                versions.Add(version);
            }

            _installedVersions[modName] = versions;
            _logService.LogDebug($"Refreshed {versions.Count} versions for {modName}");
        }
    }
}