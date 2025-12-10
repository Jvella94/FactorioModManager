using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FactorioModManager.Models.API
{
    public class ModReleaseBase
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
    }

    /// <summary>
    /// Release info from Factorio Mod Portal API
    /// https://wiki.factorio.com/Mod_portal_API#Releases
    /// </summary>
    public class ModReleaseShort : ModReleaseBase
    {
        [JsonPropertyName("info_json")]
        public ModReleaseInfoShort? InfoJson { get; set; }
    }

    public class ModReleaseFull : ModReleaseBase
    {
        [JsonPropertyName("info_json")]
        public ModReleaseInfoFull? InfoJson { get; set; }
    }

    /// <summary>
    /// Mod info embedded in a release
    /// </summary>
    public class ModReleaseInfoShort
    {
        [JsonPropertyName("factorio_version")]
        public string FactorioVersion { get; set; } = string.Empty;
    }

    public class ModReleaseInfoFull : ModReleaseInfoShort
    {
        [JsonPropertyName("dependencies")]
        public List<string>? Dependencies { get; set; }
    }
}