using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FactorioModManager.Services.API  
{
    public class FactorioApiService : IFactorioApiService  
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://mods.factorio.com/api/mods";

        public FactorioApiService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<ModDetails?> GetModDetailsAsync(string modName, string? apiKey = null)
        {
            try
            {
                var url = $"{BaseUrl}/{modName}/full?version=2.0&hide_deprecated=true";
                if (!string.IsNullOrEmpty(apiKey))
                {
                    _httpClient.DefaultRequestHeaders.Remove("Authorization");
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                }

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var portalResponse = JsonSerializer.Deserialize<ModPortalResponse>(json);

                // Convert to ModDetails
                if (portalResponse == null) return null;

                return new ModDetails
                {
                    Name = portalResponse.Name,
                    Title = portalResponse.Title,
                    Category = portalResponse.Category,
                    SourceUrl = portalResponse.SourceUrl,
                    Homepage = portalResponse.Homepage,
                    Changelog = portalResponse.Changelog,
                    DownloadsCount = portalResponse.DownloadsCount,
                    Releases = [.. portalResponse.Releases.Select(r => new ModRelease
                    {
                        Version = r.Version,
                        DownloadUrl = r.DownloadUrl,
                        ReleasedAt = r.ReleasedAt
                    })]
                };
            }
            catch (Exception ex)
            {
                LogService.Instance.LogDebug($"Error fetching mod details: {ex.Message}");
                return null;
            }
        }

        public async Task<List<string>> GetRecentlyUpdatedModsAsync(int hoursAgo, string? apiKey = null)
        {
            try
            {
                var sinceTime = DateTime.UtcNow.AddHours(-hoursAgo);
                var recentModNames = new HashSet<string>();
                var pageSize = 100;
                var maxPages = 5;

                for (int page = 1; page <= maxPages; page++)
                {
                    var url = $"{BaseUrl}?version=2.0&hide_deprecated=true&sort=updated_at&sort_order=desc&page={page}&page_size={pageSize}";
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        _httpClient.DefaultRequestHeaders.Remove("Authorization");
                        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    }

                    var response = await _httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        LogService.Instance.LogDebug($"API request failed: {response.StatusCode}");
                        break;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ModListResponse>(json);
                    if (result?.Results == null || result.Results.Count == 0)
                    {
                        break;
                    }

                    var foundRecentMod = false;
                    foreach (var mod in result.Results)
                    {
                        if (mod.LatestRelease?.ReleasedAt >= sinceTime)
                        {
                            recentModNames.Add(mod.Name);
                            foundRecentMod = true;
                        }
                    }

                    if (!foundRecentMod)
                    {
                        break;
                    }

                    await Task.Delay(100);
                }

                LogService.Instance.LogDebug($"Found {recentModNames.Count} mods updated since {sinceTime:yyyy-MM-dd HH:mm:ss} UTC");
                return [.. recentModNames];
            }
            catch (Exception ex)
            {
                LogService.Instance.LogDebug($"Error fetching recently updated mods: {ex.Message}");
                return [];
            }
        }

        public void ClearCache()
        {
            // No-op for non-cached implementation
        }

        // Internal DTOs for JSON deserialization
        private class ModPortalResponse
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [JsonPropertyName("category")]
            public string? Category { get; set; }

            [JsonPropertyName("summary")]
            public string? Summary { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("homepage")]
            public string? Homepage { get; set; }

            [JsonPropertyName("source_url")]
            public string? SourceUrl { get; set; }

            [JsonPropertyName("downloads_count")]
            public int DownloadsCount { get; set; }

            [JsonPropertyName("releases")]
            public List<ModReleaseDto> Releases { get; set; } = [];

            [JsonPropertyName("changelog")]
            public string? Changelog { get; set; }
        }

        private class ModReleaseDto
        {
            [JsonPropertyName("version")]
            public string Version { get; set; } = string.Empty;

            [JsonPropertyName("released_at")]
            public DateTime ReleasedAt { get; set; }

            [JsonPropertyName("download_url")]
            public string? DownloadUrl { get; set; }

            [JsonPropertyName("sha1")]
            public string? Sha1 { get; set; }

            [JsonPropertyName("info_json")]
            public ModInfo? InfoJson { get; set; }
        }

        private class ModListResponse
        {
            [JsonPropertyName("results")]
            public List<ModSummary> Results { get; set; } = [];

            [JsonPropertyName("pagination")]
            public PaginationInfo? Pagination { get; set; }
        }

        private class ModSummary
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("latest_release")]
            public ModReleaseDto? LatestRelease { get; set; }
        }

        private class PaginationInfo
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
}
