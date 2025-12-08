using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FactorioModManager.Services
{
    public class ModService(ISettingsService _settingsService, ILogService _logService) : IModService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        private readonly Dictionary<string, HashSet<string>> _installedVersions = new();  // modName → versions

        public string GetModsDirectory()
        {
            return _settingsService.GetModsPath(); // ✅ Uses injected settings
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

            var modFiles = Directory.GetFiles(modsDirectory, Constants.FileSystem.ModFilePattern);
            var mods = new List<(ModInfo, bool, DateTime?, string?, string)>();

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

        public async Task DownloadVersionAsync(string modName, string version, string downloadUrl)
        {
            var modsDir = _settingsService.GetModsPath();
            var exactFileName = $"{modName}_{version}.zip";
            var zipPath = Path.Combine(modsDir, $"{modName}_{version}.zip");

            using var httpClient = new HttpClient();
            using var downloadStream = await httpClient.GetStreamAsync(downloadUrl);
            using var fileStream = new FileStream(zipPath, FileMode.Create);

            var buffer = new byte[8192];
            int bytesRead;
            long totalBytes = 0;

            while ((bytesRead = await downloadStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalBytes += bytesRead;
                // Report progress to caller if needed
            }
            RefreshInstalledVersions(modName);
            _logService.Log($"Downloaded {modName} v{version} to {zipPath}");
        }

        public async Task<int> RefreshInstalledCount(string modName)
        {
            var modsDir = GetModsDirectory();
            var modFiles = Directory.GetFiles(modsDir, $"{modName}_*.zip");  // modname_*.zip
            return modFiles.Length;
        }

        public void RefreshInstalledCounts(string modName)
        {
            // Notify all ModViewModels to refresh their counts
            // Or broadcast via messenger/event aggregator
            LogService.Instance.Log($"Refresh count for {modName}");
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
            return _installedVersions.GetValueOrDefault(modName, new HashSet<string>());
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