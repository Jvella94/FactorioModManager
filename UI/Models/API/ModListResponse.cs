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
        public ModRelease? LatestRelease { get; set; }
    }

    public record PaginationInfo(
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("page_count")] int PageCount,
        [property: JsonPropertyName("page_size")] int PageSize
    );
}