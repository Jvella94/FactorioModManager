using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FactorioModManager.Models.API
{
    /// <summary>
    /// Release info from Factorio Mod Portal API
    /// https://wiki.factorio.com/Mod_portal_API#Releases
    /// </summary>
    public class ModReleaseDto
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("released_at")]
        public DateTime ReleasedAt { get; set; }

        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;  

        [JsonPropertyName("sha1")]
        public string? Sha1 { get; set; }

        [JsonPropertyName("info_json")]
        public ModInfo? InfoJson { get; set; } 
    }
}
