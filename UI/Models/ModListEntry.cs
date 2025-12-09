using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FactorioModManager.Models
{
    public class ModListEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; }
    }

    /// <summary>
    /// Used for serialising mod-list.json to find which mods are enabled/known to be installed.
    /// </summary>
    public class ModList
    {
        [JsonPropertyName("mods")]
        public List<ModListEntry> Mods { get; set; } = [];
    }
}