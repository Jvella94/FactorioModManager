using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FactorioModManager.Models
{
    public class ModGroup
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("mods")]
        public List<string> ModNames { get; set; } = new();

        [JsonPropertyName("color")]
        public string? Color { get; set; }
    }

    public class ModGroupCollection
    {
        [JsonPropertyName("groups")]
        public List<ModGroup> Groups { get; set; } = new();
    }
}
