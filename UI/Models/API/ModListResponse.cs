using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FactorioModManager.Models.API  
{
    public class ModListResponse
    {
        [JsonPropertyName("results")]
        public List<ModSummary> Results { get; set; } = [];

        [JsonPropertyName("pagination")]
        public PaginationInfo? Pagination { get; set; }
    }

    public class ModSummary
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("latest_release")]
        public ModReleaseDto? LatestRelease { get; set; }
    }

    public class PaginationInfo
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("page_count")]
        public int PageCount { get; set; }

        [JsonPropertyName("page_size")]
        public int PageSize { get; set; }
    }

}
