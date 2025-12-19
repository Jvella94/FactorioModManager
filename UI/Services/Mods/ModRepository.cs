using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace FactorioModManager.Services.Mods
{
    public interface IModRepository
    {
        List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> LoadAllMods();

        ModInfo? ReadModInfo(string filePath);

        void SaveModEntry(string modName, bool enabled, string? version = null);

        Dictionary<string, ModListEntry> LoadModEntries();

        void SaveModEntries(IDictionary<string, ModListEntry> states);
    }

    public class ModRepository(ILogService logService, Settings.IModPathSettings pathSettings) : IModRepository
    {
        private readonly ILogService _logService = logService;
        private readonly Settings.IModPathSettings _pathSettings = pathSettings;

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

            var modEntries = LoadModEntries();
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
                        var isEnabled = !modEntries.TryGetValue(modInfo.Name, out var entry) || entry.Enabled;
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
                            var isEnabled = !modEntries.TryGetValue(modInfo.Name, out var entry) || entry.Enabled;
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

        public void SaveModEntry(string modName, bool enabled, string? version = null)
        {
            var states = LoadModEntries();

            if (states.TryGetValue(modName, out var existing))
            {
                var newEntry = new ModListEntry
                {
                    Name = modName,
                    Enabled = enabled,
                    Version = version ?? existing.Version
                };
                states[modName] = newEntry;
            }
            else
            {
                states[modName] = new ModListEntry
                {
                    Name = modName,
                    Enabled = enabled,
                    Version = version
                };
            }

            SaveModEntries(states);
        }

        public Dictionary<string, ModListEntry> LoadModEntries()
        {
            var modsDirectory = _pathSettings.GetModsPath();
            var modListPath = Path.Combine(modsDirectory, Constants.FileSystem.ModListFileName);
            var modEntries = new Dictionary<string, ModListEntry>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(modListPath))
            {
                _logService.LogWarning("mod-list.json not found");
                return modEntries;
            }

            try
            {
                var json = File.ReadAllText(modListPath);
                var modList = JsonSerializer.Deserialize<ModListDto>(json);

                if (modList?.Mods != null)
                {
                    foreach (var entry in modList.Mods)
                    {
                        modEntries[entry.Name] = entry;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error reading mod-list.json: {ex.Message}", ex);
            }

            return modEntries;
        }

        public void SaveModEntries(IDictionary<string, ModListEntry> states)
        {
            var modsDirectory = _pathSettings.GetModsPath();
            var modListPath = Path.Combine(modsDirectory, Constants.FileSystem.ModListFileName);

            try
            {
                var dto = new ModListDto
                {
                    Mods = [.. states.Values.Select(e => new ModListEntry
                    {
                        Name = e.Name,
                        Enabled = e.Enabled,
                        Version = e.Version
                    })]
                };

                var json = JsonSerializer.Serialize(dto, Constants.JsonOptions.ModList);
                File.WriteAllText(modListPath, json);

                _logService.Log($"Wrote {Constants.FileSystem.ModListFileName} at {DateTime.UtcNow:O}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error writing mod-list.json: {ex.Message}", ex);
                throw;
            }
        }

        private ModInfo? ExtractModInfoFromZip(string zipPath)
        {
            const int maxAttempts = 5;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);

                    var infoEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.EndsWith(Constants.FileSystem.InfoJsonFileName, StringComparison.OrdinalIgnoreCase));

                    if (infoEntry != null)
                    {
                        using var stream = infoEntry.Open();
                        using var reader = new StreamReader(stream);
                        var json = reader.ReadToEnd();
                        return JsonSerializer.Deserialize<ModInfo>(json);
                    }

                    return null;
                }
                catch (IOException ex)
                {
                    if (attempt == maxAttempts)
                    {
                        _logService.LogError($"IO error extracting info from {zipPath} after {maxAttempts} attempts: {ex.Message}", ex);
                        break;
                    }

                    // short backoff and retry
                    Thread.Sleep(150 * attempt);
                    continue;
                }
                catch (InvalidDataException ex)
                {
                    _logService.LogError($"Invalid zip data in {zipPath}: {ex.Message}", ex);
                    break;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error extracting info from {zipPath}: {ex.Message}", ex);
                    break;
                }
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
            const int maxAttempts = 5;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);

                    var thumbnailEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.EndsWith("thumbnail.png", StringComparison.OrdinalIgnoreCase));

                    if (thumbnailEntry != null)
                    {
                        return $"{zipPath}|{thumbnailEntry.FullName}";
                    }

                    return null;
                }
                catch (IOException ex)
                {
                    if (attempt == maxAttempts)
                    {
                        _logService.LogError($"IO error finding thumbnail in {zipPath} after {maxAttempts} attempts: {ex.Message}", ex);
                        break;
                    }

                    Thread.Sleep(150 * attempt);
                    continue;
                }
                catch (InvalidDataException ex)
                {
                    _logService.LogError($"Invalid zip data in {zipPath}: {ex.Message}", ex);
                    break;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error finding thumbnail in {zipPath}: {ex.Message}", ex);
                    break;
                }
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