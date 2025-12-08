using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FactorioModManager.Services
{
    public class ModMetadata
    {
        public string ModName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? SourceUrl { get; set; }
        public bool SourceUrlChecked { get; set; }
        public DateTime? LastChecked { get; set; }
        public string? LatestVersion { get; set; }
        public DateTime? LastUpdateCheck { get; set; }
        public bool HasUpdate { get; set; }
    }

    public class ModMetadataCollection
    {
        public List<ModMetadata> Metadata { get; set; } = [];
    }

    public class ModMetadataService : IModMetadataService
    {
        private readonly string _metadataPath;
        private Dictionary<string, ModMetadata> _cache = [];
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        public ModMetadataService()
        {
            var modsDir = ModPathHelper.GetModsDirectory();
            _metadataPath = Path.Combine(modsDir, "mod-metadata.json");
            LoadMetadata();
        }

        private void LoadMetadata()
        {
            if (!File.Exists(_metadataPath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_metadataPath);
                var collection = JsonSerializer.Deserialize<ModMetadataCollection>(json);
                if (collection?.Metadata != null)
                {
                    _cache = collection.Metadata.ToDictionary(m => m.ModName);
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.LogDebug($"Error loading metadata: {ex.Message}");
            }
        }

        private void SaveMetadata()
        {
            try
            {
                var collection = new ModMetadataCollection
                {
                    Metadata = [.. _cache.Values]
                };
                var json = JsonSerializer.Serialize(collection, SerializerOptions);
                File.WriteAllText(_metadataPath, json);
            }
            catch (Exception ex)
            {
                LogService.Instance.LogDebug($"Error saving metadata: {ex.Message}");
            }
        }

        private ModMetadata GetOrCreate(string modName)
        {
            if (!_cache.TryGetValue(modName, out var metadata))
            {
                metadata = new ModMetadata { ModName = modName };
                _cache[modName] = metadata;
            }
            return metadata;
        }

        // ADDED: Ensure all loaded mods have metadata entries
        public void EnsureModsExist(IEnumerable<string> modNames)
        {
            bool needsSave = false;
            foreach (var modName in modNames)
            {
                if (!_cache.ContainsKey(modName))
                {
                    _cache[modName] = new ModMetadata { ModName = modName };
                    needsSave = true;
                }
            }

            if (needsSave)
            {
                SaveMetadata();
            }
        }

        public void UpdateCategory(string modName, string? category)
        {
            var metadata = GetOrCreate(modName);
            metadata.Category = category;
            metadata.LastChecked = DateTime.UtcNow;
            SaveMetadata();
        }

        public void UpdateSourceUrl(string modName, string? sourceUrl, bool wasChecked = true)
        {
            var metadata = GetOrCreate(modName);
            metadata.SourceUrl = sourceUrl;
            metadata.SourceUrlChecked = wasChecked;
            metadata.LastChecked = DateTime.UtcNow;
            SaveMetadata();

            LogService.Instance.LogDebug($"Saved source URL for {modName}: {sourceUrl ?? "null"}");
            LogService.Instance.Log($"Saved source URL for {modName}: {sourceUrl ?? "(none)"}");
        }

        public string? GetCategory(string modName)
        {
            return _cache.TryGetValue(modName, out var metadata) ? metadata.Category : null;
        }

        public string? GetSourceUrl(string modName)
        {
            return _cache.TryGetValue(modName, out var metadata) ? metadata.SourceUrl : null;
        }

        public bool NeedsSourceUrlCheck(string modName)
        {
            if (!_cache.TryGetValue(modName, out var metadata))
            {
                return true;
            }

            if (metadata.SourceUrlChecked)
            {
                return false;
            }

            if (metadata.LastChecked.HasValue &&
                (DateTime.UtcNow - metadata.LastChecked.Value).TotalDays < 7)
            {
                return false;
            }

            return true;
        }

        public bool NeedsCategoryCheck(string modName)
        {
            if (!_cache.TryGetValue(modName, out var metadata))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(metadata.Category))
            {
                return false;
            }

            if (metadata.LastChecked.HasValue &&
                (DateTime.UtcNow - metadata.LastChecked.Value).TotalDays < 7)
            {
                return false;
            }

            return true;
        }

        public void MarkAsChecked(string modName)
        {
            var metadata = GetOrCreate(modName);
            metadata.LastChecked = DateTime.UtcNow;
            metadata.SourceUrlChecked = true;
            SaveMetadata();
        }

        public string? GetLatestVersion(string modName)
        {
            return _cache.TryGetValue(modName, out var metadata) ? metadata.LatestVersion : null;
        }

        // ADDED: Get update status
        public bool GetHasUpdate(string modName)
        {
            return _cache.TryGetValue(modName, out var metadata) && metadata.HasUpdate;
        }

        // ADDED: Clear update flag (after successful update)
        public void ClearUpdate(string modName)
        {
            if (_cache.TryGetValue(modName, out var metadata))
            {
                metadata.HasUpdate = false;
                metadata.LatestVersion = null;
                SaveMetadata();
                LogService.Instance.LogDebug($"Cleared update flag for {modName}");
            }
        }

        // ADDED: Mark multiple mods as updated (batch operation)
        public void UpdateLatestVersion(string modName, string version, bool hasUpdate)
        {
            var metadata = GetOrCreate(modName);
            metadata.LatestVersion = version;
            metadata.HasUpdate = hasUpdate;
            metadata.LastUpdateCheck = DateTime.UtcNow;
            SaveMetadata();
        }

        // ADDED: Clear all update flags (useful for forcing fresh update check)
        public void ClearAllUpdates()
        {
            foreach (var metadata in _cache.Values)
            {
                metadata.HasUpdate = false;
            }
            SaveMetadata();
            LogService.Instance.LogDebug("Cleared all update flags");
        }
    }
}