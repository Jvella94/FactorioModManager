using FactorioModManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FactorioModManager.Services
{
    public class FactorioApiService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://mods.factorio.com/api/mods";

        public FactorioApiService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<ModPortalResponse?> GetModDetailsAsync(string modName, string? apiKey = null)
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

                var json = await response.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ModPortalResponse>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching mod details: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> CheckForUpdatesAsync(string modName, string currentVersion, string? apiKey = null)
        {
            var details = await GetModDetailsAsync(modName, apiKey);
            if (details?.Releases == null || details.Releases.Count == 0)
            {
                return false;
            }

            var latestRelease = details.Releases[0];
            return CompareVersions(latestRelease.Version, currentVersion) > 0;
        }

        private int CompareVersions(string v1, string v2)
        {
            var parts1 = v1.Split('.').Select(int.Parse).ToArray();
            var parts2 = v2.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
            {
                var p1 = i < parts1.Length ? parts1[i] : 0;
                var p2 = i < parts2.Length ? parts2[i] : 0;

                if (p1 > p2) return 1;
                if (p1 < p2) return -1;
            }

            return 0;
        }
    }

    public class ModPortalResponse
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
        public List<ModRelease> Releases { get; set; } = new();

        [JsonPropertyName("changelog")]
        public string? Changelog { get; set; }
    }

    public class ModRelease
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
}
