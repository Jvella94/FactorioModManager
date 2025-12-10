using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace FactorioModManager.Services
{
    public interface IModMetadataService
    {
        void ClearAllModMetadata();

        void ClearAllModUpdates();

        void ClearMetadataForMod(string modName);

        void ClearModUpdateInfo(string modName);

        void CreateBaseMetadata(string modName);

        void EnsureModsExist(IEnumerable<string> modNames);

        string? GetCategory(string modName);

        bool GetHasUpdate(string modName);

        string? GetLatestVersion(string modName);

        string? GetSourceUrl(string modName);

        bool NeedsMetadaUpdate(string modName);

        void SaveNow();

        void UpdateAllPortalMetadata(string modName, string? category, string? sourceUrl);

        void UpdateCategory(string modName, string? category);

        void UpdateLatestVersion(string modName, string version, bool hasUpdate);

        void UpdateSourceUrl(string modName, string? sourceUrl, bool wasChecked = true);
    }

    public class ModMetadata
    {
        public string ModName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public DateTime? CreatedOn { get; set; }
        public bool HasUpdate { get; set; }
        public DateTime? LastUpdateCheck { get; set; }
        public string? LatestVersion { get; set; }
        public string? SourceUrl { get; set; }
    }

    public class ModMetadataCollection
    {
        public List<ModMetadata> Metadata { get; set; } = [];
    }

    public class ModMetadataService : IModMetadataService
    {
        private readonly ILogService _logService;
        private readonly string _metadataPath;
        private readonly Timer _saveTimer;
        private Dictionary<string, ModMetadata> _cache = [];
        private bool _isDirty = false;

        public ModMetadataService(ILogService logService)
        {
            _logService = logService;
            // Auto-save every 5 seconds if dirty
            _saveTimer = new Timer(AutoSave, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            var modsDir = FolderPathHelper.GetModsDirectory();
            _metadataPath = Path.Combine(modsDir, "mod-metadata.json");
            LoadMetadata();
        }

        public void ClearAllModMetadata()
        {
            _cache.Clear();
            SaveNow();
            _logService.LogDebug("Cleared all metadata.");
        }

        public void ClearAllModUpdates()
        {
            foreach (var metadata in _cache.Values)
            {
                ClearUpdateMetadata(metadata);
            }
            SaveNow();
            _logService.LogDebug("Cleared all update flags.");
        }

        public void ClearMetadataForMod(string modName)
        {
            if (_cache.Remove(modName))
            {
                MarkDirty();
                _logService.LogDebug($"Cleared metadata for mod: {modName}");
            }
        }

        public void ClearModUpdateInfo(string modName)
        {
            if (_cache.TryGetValue(modName, out var metadata))
            {
                ClearUpdateMetadata(metadata);
            }
        }

        /// <summary>
        /// Fallback method in case other metadata cannot be loaded for some reason.
        /// </summary>
        /// <param name="modName"></param>
        public void CreateBaseMetadata(string modName)
        {
            var metadata = GetOrCreate(modName);
            metadata.CreatedOn = DateTime.UtcNow;
            MarkDirty();
        }

        public void Dispose() => _saveTimer?.Dispose();

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
                MarkDirty();
            }
        }

        public string? GetCategory(string modName)
        {
            return _cache.TryGetValue(modName, out var metadata) ? metadata.Category : null;
        }

        public bool GetHasUpdate(string modName)
        {
            return _cache.TryGetValue(modName, out var metadata) && metadata.HasUpdate;
        }

        public string? GetLatestVersion(string modName)
        {
            return _cache.TryGetValue(modName, out var metadata) ? metadata.LatestVersion : null;
        }

        public string? GetSourceUrl(string modName)
        {
            return _cache.TryGetValue(modName, out var metadata) ? metadata.SourceUrl : null;
        }

        public bool NeedsMetadaUpdate(string modName)
        {
            if (!_cache.TryGetValue(modName, out var metadata))
            {
                return true;
            }

            if (metadata.CreatedOn.HasValue &&
                (DateTime.UtcNow - metadata.CreatedOn.Value).TotalDays < Constants.Cache.MetadataCacheLifetime.TotalDays)
            {
                return false;
            }

            return true;
        }

        // Add explicit save for critical operations
        public void SaveNow() => SaveMetadata();

        public void UpdateCategory(string modName, string? category)
        {
            UpdatePortalMetadata(modName, category: category);
        }

        public void UpdateLatestVersion(string modName, string version, bool hasUpdate)
        {
            var metadata = GetOrCreate(modName);
            metadata.LatestVersion = version;
            metadata.HasUpdate = hasUpdate;
            metadata.LastUpdateCheck = DateTime.UtcNow;
            MarkDirty();
        }

        public void UpdateSourceUrl(string modName, string? sourceUrl, bool wasChecked = true)
        {
            UpdatePortalMetadata(modName, sourceUrl: sourceUrl);
        }

        public void UpdateAllPortalMetadata(string modName, string? category, string? sourceUrl)
        {
            UpdatePortalMetadata(modName, category, sourceUrl);
        }

        private void AutoSave(object? state)
        {
            if (_isDirty)
            {
                SaveMetadata();
                _isDirty = false;
            }
        }

        private void ClearUpdateMetadata(ModMetadata metadata)
        {
            metadata.HasUpdate = false;
            metadata.LatestVersion = null;
            MarkDirty();
            _logService.LogDebug($"Cleared update flag for {metadata.ModName}");
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

        private void MarkDirty() => _isDirty = true;

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

        private void UpdatePortalMetadata(
            string modName,
            string? category = null,
            string? sourceUrl = null)
        {
            var metadata = GetOrCreate(modName);

            if (category != null)
            {
                metadata.Category = category;
            }

            if (sourceUrl != null)
            {
                metadata.SourceUrl = sourceUrl;
            }

            metadata.CreatedOn = DateTime.UtcNow;
            MarkDirty();

            _logService.LogDebug($"Updated metadata for {modName}: " +
                $"Category = {metadata.Category ?? "(none)"}, " +
                $"SourceUrl = {metadata.SourceUrl ?? "(none)"}");
        }
    }
}