using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace FactorioModManager.Services
{
    public class ModService : IModService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> LoadAllMods()
        {
            var modsDirectory = ModPathHelper.GetModsDirectory();
            var modListPath = Path.Combine(modsDirectory, Constants.FileSystem.ModListFileName);

            var enabledMods = new Dictionary<string, bool>();
            if (File.Exists(modListPath))
            {
                var jsonString = File.ReadAllText(modListPath);
                var modListData = JsonSerializer.Deserialize<ModListData>(jsonString);
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
                            var thumbnailPath = FindThumbnail(archive,modFile);

                            mods.Add((modInfo, isEnabled, lastModified, thumbnailPath, modFile));
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Log($"Error loading mod {Path.GetFileName(modFile)}: {ex.Message}",
                        LogLevel.Error);
                }
            }

            return mods;
        }

        private static string? FindThumbnail(ZipArchive archive,string modFile)
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
            var modsDirectory = ModPathHelper.GetModsDirectory();
            var modListPath = Path.Combine(modsDirectory, Constants.FileSystem.ModListFileName);

            ModListData modListData;
            if (File.Exists(modListPath))
            {
                var jsonString = File.ReadAllText(modListPath);
                modListData = JsonSerializer.Deserialize<ModListData>(jsonString) ?? new ModListData();
            }
            else
            {
                modListData = new ModListData();
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

            LogService.Instance.Log($"Mod '{modName}' {(enabled ? "enabled" : "disabled")}");
        }

        public void RemoveMod(string modName)
        {
            var modsDirectory = ModPathHelper.GetModsDirectory();
            var modFiles = Directory.GetFiles(modsDirectory, $"{modName}_*.zip");

            foreach (var file in modFiles)
            {
                File.Delete(file);
                LogService.Instance.Log($"Removed mod file: {Path.GetFileName(file)}");
            }
        }

        private class ModListData
        {
            public List<ModListEntry> Mods { get; set; } = [];
        }
    }
}
