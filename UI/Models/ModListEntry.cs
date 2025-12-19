using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FactorioModManager.Models
{
    // Immutable DTO for a single mod entry in mod-list.json
    public record ModListEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; }

        // Optional active version stored in mod-list.json
        [JsonPropertyName("version")]
        public string? Version { get; init; }
    }

    // Container for serialising/deserialising mod-list.json
    public record ModListDto
    {
        [JsonPropertyName("mods")]
        public List<ModListEntry> Mods { get; set; } = [];
    }
}