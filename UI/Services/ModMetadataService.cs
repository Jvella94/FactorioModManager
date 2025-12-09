using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

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
        private bool _isDirty = false;
        private readonly Timer _saveTimer;
        private readonly string _metadataPath;
        private Dictionary<string, ModMetadata> _cache = [];
        private readonly ILogService _logService;

        public ModMetadataService(ILogService logService)
        {
            _logService = logService;
            // Auto-save every 5 seconds if dirty
            _saveTimer = new Timer(AutoSave, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            var modsDir = ModPathHelper.GetModsDirectory();
            _metadataPath = Path.Combine(modsDir, "mod-metadata.json");
            LoadMetadata();
        }

        private void MarkDirty() => _isDirty = true;

        private void AutoSave(object? state)
        {
            if (_isDirty)
            {
                SaveMetadata();
                _isDirty = false;
            }
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
                _logService.LogError($"Error loading metadata: {ex.Message}", ex);
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
                var json = JsonSerializer.Serialize(collection, Constants.JsonHelper.IndentedOnly);
                File.WriteAllText(_metadataPath, json);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error saving metadata: {ex.Message}", ex);
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
            MarkDirty();

            _logService.LogDebug($"Saved category for {modName}: {category ?? "(none)"}");
        }

        public void UpdateSourceUrl(string modName, string? sourceUrl, bool wasChecked = true)
        {
            var metadata = GetOrCreate(modName);
            metadata.SourceUrl = sourceUrl;
            metadata.SourceUrlChecked = wasChecked;
            metadata.LastChecked = DateTime.UtcNow;
            MarkDirty();

            _logService.LogDebug($"Saved source URL for {modName}: {sourceUrl ?? "(none)"}");
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

        public bool GetHasUpdate(string modName)
        {
            return _cache.TryGetValue(modName, out var metadata) && metadata.HasUpdate;
        }

        public void ClearUpdate(string modName)
        {
            if (_cache.TryGetValue(modName, out var metadata))
            {
                metadata.HasUpdate = false;
                metadata.LatestVersion = null;
                SaveMetadata();
                _logService.LogDebug($"Cleared update flag for {modName}");
            }
        }

        public void UpdateLatestVersion(string modName, string version, bool hasUpdate)
        {
            var metadata = GetOrCreate(modName);
            metadata.LatestVersion = version;
            metadata.HasUpdate = hasUpdate;
            metadata.LastUpdateCheck = DateTime.UtcNow;
            SaveMetadata();
        }

        public void ClearAllUpdates()
        {
            foreach (var metadata in _cache.Values)
            {
                metadata.HasUpdate = false;
            }
            SaveMetadata();
            _logService.LogDebug("Cleared all update flags");
        }

        // Add explicit save for critical operations
        public void SaveNow() => SaveMetadata();

        public void Dispose() => _saveTimer?.Dispose();
    }
}