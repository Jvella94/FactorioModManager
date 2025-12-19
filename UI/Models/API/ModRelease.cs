using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FactorioModManager.Models.API
{
    /// <summary>
    /// Base class for release info from Factorio Mod Portal API
    /// https://wiki.factorio.com/Mod_portal_API#Releases
    /// </summary>
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
    /// Release info from Factorio Mod Portal API using the short endpoint
    /// </summary>
    public class ModReleaseShort : ModReleaseBase
    {
        [JsonPropertyName("info_json")]
        public ModReleaseInfoShort? InfoJson { get; set; }
    }

    /// <summary>
    /// Represents a mod release that includes detailed information about the release, extending the short mod release
    /// functionality.
    /// </summary>
    public class ModReleaseFull : ModReleaseBase
    {
        [JsonPropertyName("info_json")]
        public ModReleaseInfoFull? InfoJson { get; set; }
    }

    /// <summary>
    /// Represents summary information about a mod release, including the supported Factorio version.
    /// </summary>
    public class ModReleaseInfoShort
    {
        [JsonPropertyName("factorio_version")]
        public string FactorioVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents detailed information about a mod release, including its dependencies.
    /// </summary>
    public class ModReleaseInfoFull : ModReleaseInfoShort
    {
        [JsonPropertyName("dependencies")]
        public List<string>? Dependencies { get; set; }
    }
}