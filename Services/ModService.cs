using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using FactorioModManager.Models;
using System.Diagnostics;

namespace FactorioModManager.Services
{
    public class ModService
    {
        private readonly string _modsDirectory;
        private readonly string _modListPath;
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        public ModService()
        {
            _modsDirectory = ModPathHelper.GetModsDirectory();
            _modListPath = ModPathHelper.GetModListPath();
        }

        public List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string ThumbnailPath)> LoadAllMods()
        {
            if (!Directory.Exists(_modsDirectory))
            {
                throw new DirectoryNotFoundException($"Mods directory not found: {_modsDirectory}");
            }

            var modList = LoadModList();
            var mods = new List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string ThumbnailPath)>();

            // Load mods from folders
            foreach (var modDir in Directory.GetDirectories(_modsDirectory))
            {
                var infoPath = Path.Combine(modDir, "info.json");
                if (File.Exists(infoPath))
                {
                    var modInfo = LoadModInfo(infoPath);
                    var isEnabled = modList.FirstOrDefault(m => m.Name == modInfo.Name)?.Enabled ?? false;
                    var lastUpdated = Directory.GetLastWriteTime(modDir);
                    var thumbnailPath = Path.Combine(modDir, "thumbnail.png");

                    if (!File.Exists(thumbnailPath))
                    {
                        thumbnailPath = string.Empty;
                    }

                    mods.Add((modInfo, isEnabled, lastUpdated, thumbnailPath));
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

                            // Store zip path with entry for lazy loading
                            var thumbnailEntry = archive.Entries.FirstOrDefault(e =>
                                e.FullName.EndsWith("thumbnail.png", StringComparison.OrdinalIgnoreCase));
                            var thumbnailPath = thumbnailEntry != null ? $"{zipFile}|{thumbnailEntry.FullName}" : string.Empty;

                            mods.Add((modInfo, isEnabled, lastUpdated, thumbnailPath));
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogDebug($"Error loading mod from {zipFile}: {ex.Message}");
                }
            }

            return mods;
        }

        // FIXED: Made static (CA1822)
        private static ModInfo LoadModInfo(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ModInfo>(json)
                ?? throw new InvalidDataException($"Failed to parse mod info: {path}");
        }

        private List<ModListEntry> LoadModList()
        {
            if (!File.Exists(_modListPath))
            {
                return [];
            }

            var json = File.ReadAllText(_modListPath);
            var modList = JsonSerializer.Deserialize<ModList>(json);
            return modList?.Mods ?? [];
        }

        public void SaveModList(List<ModListEntry> mods)
        {
            var modList = new ModList { Mods = mods };
            var json = JsonSerializer.Serialize(modList, SerializerOptions);
            File.WriteAllText(_modListPath, json);
        }

        public void ToggleMod(string modName, bool enabled)
        {
            var modList = LoadModList();
            var entry = modList.FirstOrDefault(m => m.Name == modName);

            if (entry != null)
            {
                entry.Enabled = enabled;
            }
            else
            {
                modList.Add(new ModListEntry { Name = modName, Enabled = enabled });
            }

            SaveModList(modList);
        }
    }
}
