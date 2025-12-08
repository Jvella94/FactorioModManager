using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FactorioModManager.Models
{
    /// <summary>
    /// The tags found in mods info.json files to get more details about the mod.
    /// </summary>
    public class ModInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        [JsonPropertyName("contact")]
        public string? Contact { get; set; }

        [JsonPropertyName("homepage")]
        public string? Homepage { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("factorio_version")]
        public string FactorioVersion { get; set; } = "2.0";

        [JsonPropertyName("dependencies")]
        public List<string> Dependencies { get; set; } = [];
    }
}
