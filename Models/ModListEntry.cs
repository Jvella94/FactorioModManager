using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FactorioModManager.Models
{
    public class ModListEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }

    public class ModList
    {
        [JsonPropertyName("mods")]
        public List<ModListEntry> Mods { get; set; } = [];
    }
}
