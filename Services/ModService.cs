using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using FactorioModManager.Models;

namespace FactorioModManager.Services
{
    public class ModService
    {
        private readonly string _modsDirectory;
        private readonly string _modListPath;
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public ModService()
        {
            _modsDirectory = ModPathHelper.GetModsDirectory();
            _modListPath = ModPathHelper.GetModListPath();
        }

        public List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath)> LoadAllMods()
        {
            if (!Directory.Exists(_modsDirectory))
            {
                LogService.LogDebug($"Mods directory not found: {_modsDirectory}");
                return [];
            }

            var modList = LoadModList();
            var mods = new List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath)>();

            // Load mods from folders
            foreach (var modDir in Directory.GetDirectories(_modsDirectory))
            {
                var infoPath = Path.Combine(modDir, "info.json");
                if (File.Exists(infoPath))
                {
                    try
                    {
                        var modInfo = LoadModInfo(infoPath);
                        var isEnabled = modList.FirstOrDefault(m => m.Name == modInfo.Name)?.Enabled ?? false;
                        var lastUpdated = Directory.GetLastWriteTime(modDir);
                        var thumbnailPath = Path.Combine(modDir, "thumbnail.png");

                        if (!File.Exists(thumbnailPath))
                        {
                            thumbnailPath = null;
                        }

                        mods.Add((modInfo, isEnabled, lastUpdated, thumbnailPath));
                    }
                    catch (Exception ex)
                    {
                        LogService.LogDebug($"Error loading mod from folder {modDir}: {ex.Message}");
                    }
                }
            }

            // Load mods from zip files
            foreach (var zipFile in Directory.GetFiles(_modsDirectory, "*.zip"))
            {
                try
                {
                    using var archive = ZipFile.OpenRead(zipFile);
                    var infoEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.EndsWith("info.json", StringComparison.OrdinalIgnoreCase));

                    if (infoEntry != null)
                    {
                        using var stream = infoEntry.Open();
                        var modInfo = JsonSerializer.Deserialize<ModInfo>(stream);

                        if (modInfo != null)
                        {
                            var isEnabled = modList.FirstOrDefault(m => m.Name == modInfo.Name)?.Enabled ?? false;
                            var lastUpdated = File.GetLastWriteTime(zipFile);

                            // Extract thumbnail to temp folder for performance
                            var thumbnailPath = ExtractThumbnail(zipFile, archive);

                            mods.Add((modInfo, isEnabled, lastUpdated, thumbnailPath));
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogDebug($"Error loading mod from zip {zipFile}: {ex.Message}");
                }
            }

            return mods;
        }

        private static ModInfo LoadModInfo(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ModInfo>(json)
                ?? throw new InvalidDataException($"Failed to parse mod info: {path}");
        }

        private static string? ExtractThumbnail(string zipFilePath, ZipArchive archive)
        {
            try
            {
                var thumbnailEntry = archive.Entries.FirstOrDefault(e =>
                    e.Name.Equals("thumbnail.png", StringComparison.OrdinalIgnoreCase));

                if (thumbnailEntry != null)
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), "FactorioModManager", "Thumbnails");
                    Directory.CreateDirectory(tempPath);

                    var thumbnailFile = Path.Combine(tempPath, $"{Path.GetFileNameWithoutExtension(zipFilePath)}_thumbnail.png");

                    // Only extract if not already cached
                    if (!File.Exists(thumbnailFile))
                    {
                        thumbnailEntry.ExtractToFile(thumbnailFile, overwrite: true);
                    }

                    return thumbnailFile;
                }
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"Error extracting thumbnail from {zipFilePath}: {ex.Message}");
            }

            return null;
        }

        private List<ModListEntry> LoadModList()
        {
            if (!File.Exists(_modListPath))
            {
                LogService.LogDebug($"mod-list.json not found at {_modListPath}");
                return [];
            }

            try
            {
                var json = File.ReadAllText(_modListPath);
                var modList = JsonSerializer.Deserialize<ModList>(json);
                return modList?.Mods ?? [];
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"Error reading mod-list.json: {ex.Message}");
                return [];
            }
        }

        private void SaveModList(List<ModListEntry> mods)
        {
            try
            {
                var modList = new ModList { Mods = mods };
                var json = JsonSerializer.Serialize(modList, JsonOptions);
                File.WriteAllText(_modListPath, json);
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"Error saving mod-list.json: {ex.Message}");
            }
        }

        public void ToggleMod(string modName, bool enabled)
        {
            var modList = LoadModList();
            var entry = modList.FirstOrDefault(m => m.Name == modName);

            if (entry != null)
            {
                entry.Enabled = enabled;
                LogService.LogDebug($"Toggled {modName} to {(enabled ? "enabled" : "disabled")}");
            }
            else
            {
                // Add new entry if mod not in list
                modList.Add(new ModListEntry { Name = modName, Enabled = enabled });
                LogService.LogDebug($"Added {modName} to mod-list.json with enabled={enabled}");
            }

            SaveModList(modList);
        }
    }
}
