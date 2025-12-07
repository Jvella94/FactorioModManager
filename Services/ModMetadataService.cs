using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FactorioModManager.Models;

namespace FactorioModManager.Services
{
    public class ModMetadata
    {
        public string ModName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? SourceUrl { get; set; }
        public bool SourceUrlChecked { get; set; }
        public DateTime? LastChecked { get; set; }
    }

    public class ModMetadataCollection
    {
        public List<ModMetadata> Metadata { get; set; } = new();
    }

    public class ModMetadataService
    {
        private readonly string _metadataPath;
        private Dictionary<string, ModMetadata> _cache = new();

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
                Console.WriteLine($"Error loading metadata: {ex.Message}");
            }
        }

        public void SaveMetadata()
        {
            try
            {
                var collection = new ModMetadataCollection
                {
                    Metadata = _cache.Values.ToList()
                };

                var json = JsonSerializer.Serialize(collection, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_metadataPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving metadata: {ex.Message}");
            }
        }

        public ModMetadata GetOrCreate(string modName)
        {
            if (!_cache.ContainsKey(modName))
            {
                _cache[modName] = new ModMetadata { ModName = modName };
            }
            return _cache[modName];
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
            return !metadata.SourceUrlChecked;
        }

        public bool NeedsCategoryCheck(string modName)
        {
            if (!_cache.TryGetValue(modName, out var metadata))
            {
                return true;
            }
            return string.IsNullOrEmpty(metadata.Category);
        }
    }
}
