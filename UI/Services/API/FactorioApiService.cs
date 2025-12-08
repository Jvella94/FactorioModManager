using FactorioModManager.Models.API;
using FactorioModManager.Models.DTO;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FactorioModManager.Services.API
{
    public class FactorioApiService(HttpClient httpClient) : IFactorioApiService
    {
        private readonly HttpClient _httpClient = httpClient;
        private const string BaseUrl = "https://mods.factorio.com/api/mods";

        public async Task<ModDetailsShortDTO?> GetModDetailsAsync(string modName)
        {
            try
            {
                var url = $"{BaseUrl}/{modName}?version=2.0&hide_deprecated=true";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var portalResponse = JsonSerializer.Deserialize<ModDetailsShort>(json);

                // Convert to ModDetailsDTO
                if (portalResponse == null) return null;
                return portalResponse.ToShortDTO();
            }
            catch (Exception ex)
            {
                LogService.Instance.LogDebug($"Error fetching mod details: {ex.Message}");
                return null;
            }
        }

        public async Task<ModDetailsFullDTO?> GetModDetailsFullAsync(string modName)
        {
            try
            {
                var url = $"{BaseUrl}/{modName}/full?version=2.0&hide_deprecated=true";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var portalResponse = JsonSerializer.Deserialize<ModDetailsFull>(json);

                // Convert to ModDetails
                if (portalResponse == null) return null;
                return portalResponse.ToFullDTO();
            }
            catch (Exception ex)
            {
                LogService.Instance.LogDebug($"Error fetching mod details: {ex.Message}");
                return null;
            }
        }

        public async Task<List<string>> GetRecentlyUpdatedModsAsync(int hoursAgo)
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
    }
}