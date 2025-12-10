using FactorioModManager.Models;
using FactorioModManager.Models.API;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace FactorioModManager.Services.Mods
{
    public class ModRepository : IModRepository
    {
        private readonly ILogService _logService;
        private readonly Settings.IModPathSettings _pathSettings;

        public ModRepository(ILogService logService, Settings.IModPathSettings pathSettings)
        {
            _logService = logService;
            _pathSettings = pathSettings;
        }

        public List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> LoadAllMods()
        {
            var modsDirectory = _pathSettings.GetModsPath();
            var modListPath = Path.Combine(modsDirectory, Constants.FileSystem.ModListFileName);

            _logService.Log($"Loading mods from: {modsDirectory}");

            if (!Directory.Exists(modsDirectory))
            {
                _logService.LogWarning($"Mods directory not found: {modsDirectory}");
                return [];
            }

            var enabledStates = LoadEnabledStates();
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

            _logService.Log($"Loaded {mods.Count} total mods");
            return mods;
        }

        public ModInfo? ReadModInfo(string filePath)
        {
            try
            {
                if (filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return ExtractModInfoFromZip(filePath);
                }

                var infoPath = Path.Combine(filePath, Constants.FileSystem.InfoJsonFileName);
                if (File.Exists(infoPath))
                    return LoadModInfoFromJson(infoPath);

                _logService.LogWarning($"info.json not found for {filePath}");
                return null;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error reading ModInfo from {filePath}: {ex.Message}", ex);
                return null;
            }
        }

        public void SaveModState(string modName, bool enabled)
        {
            var states = LoadEnabledStates();
            states[modName] = enabled;
            SaveEnabledStates(states);
        }

        public Dictionary<string, bool> LoadEnabledStates()
        {
            var modsDirectory = _pathSettings.GetModsPath();
            var modListPath = Path.Combine(modsDirectory, Constants.FileSystem.ModListFileName);
            var enabledStates = new Dictionary<string, bool>();

            if (!File.Exists(modListPath))
            {
                _logService.LogWarning("mod-list.json not found");
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

        public void SaveEnabledStates(Dictionary<string, bool> states)
        {
            var modsDirectory = _pathSettings.GetModsPath();
            var modListPath = Path.Combine(modsDirectory, Constants.FileSystem.ModListFileName);

            try
            {
                var modList = new ModList
                {
                    Mods = [.. states.Select(kvp => new ModListEntry
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
    }
}
