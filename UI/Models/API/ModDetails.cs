using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FactorioModManager.Models.API
{
    /// <summary>
    /// Response from /api/mods/{name} (Short endpoint)
    /// </summary>
    public class ModDetailsShort
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("owner")]
        public string Owner { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("downloads_count")]
        public int DownloadsCount { get; set; }

        [JsonPropertyName("releases")]
        public List<ModReleaseShort> Releases { get; set; } = [];

        [JsonPropertyName("thumbnail")]
        public string? Thumbnail { get; set; }

        [JsonPropertyName("score")]
        public float? Score { get; set; }  // Only when not 0
    }

    /// <summary>
    /// Response from /api/mods/{name}/full (Full endpoint)
    /// </summary>
    public class ModDetailsFull : ModDetailsShort
    {
        [JsonPropertyName("changelog")]
        public string? Changelog { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("last_highlighted_at")]
        public DateTime? LastHighlightedAt { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("source_url")]
        public string? SourceUrl { get; set; }

        [JsonPropertyName("homepage")]
        public string? Homepage { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = [];

        [JsonPropertyName("license")]
        public License? License { get; set; }

        [JsonPropertyName("deprecated")]
        public bool? Deprecated { get; set; }

        [JsonPropertyName("releases")]
        public new List<ModReleaseFull> Releases { get; set; } = [];
    }

    /// <summary>
    /// License information for a mod
    /// https://wiki.factorio.com/Mod_portal_API#License
    /// </summary>
    public class License
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }  // Usually URL to full license text
    }
}