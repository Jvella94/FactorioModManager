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
        public List<string> ModNames { get; set; } = [];

        [JsonPropertyName("color")]
        public string? Color { get; set; }
    }

    /// <summary>
    /// Represents the mod groups made by the user in mod-groups.json.
    /// </summary>
    public class ModGroupCollection
    {
        [JsonPropertyName("groups")]
        public List<ModGroup> Groups { get; set; } = [];
    }
}