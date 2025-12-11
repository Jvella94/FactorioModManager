using FactorioModManager.Models.API;
using FactorioModManager.Models.DTO;
using FactorioModManager.Models.Mapping;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FactorioModManager.Services.API
{
    public interface IFactorioApiService
    {
        Task<ModDetailsShortDTO?> GetModDetailsAsync(string modName);

        Task<ModDetailsFullDTO?> GetModDetailsFullAsync(string modName);

        // Added isManual parameter - when true, do a full scan and do NOT stop at the last cached known update
        Task<List<string>> GetRecentlyUpdatedModsAsync(int hoursAgo, bool isManual = false);

        void ClearCache();

        /// <summary>
        /// Downloads a mod version from the Factorio mod portal
        /// </summary>
        /// <param name="downloadUrl">Full download URL from the mod portal API</param>
        /// <param name="destinationPath">Local file path where the mod should be saved</param>
        /// <param name="progress">Optional progress callback (bytesDownloaded, totalBytes)</param>
        /// <param name="cancellationToken">Cancellation token to abort download</param>
        Task DownloadModAsync(
            string downloadUrl,
            string destinationPath,
            IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null,
            CancellationToken cancellationToken = default);
    }

    public class FactorioApiService(HttpClient httpClient, ILogService logService) : IFactorioApiService
    {
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private readonly ILogService _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private string _lastModName = string.Empty;
        private const string _baseUrl = "https://mods.factorio.com/api/mods";

        public async Task<ModDetailsShortDTO?> GetModDetailsAsync(string modName)
        {
            try
            {
                var url = $"{_baseUrl}/{modName}?version=2.0&hide_deprecated=true";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var portalResponse = JsonSerializer.Deserialize<ModDetailsShort>(json);

                if (portalResponse == null) return null;
                return portalResponse.ToShortDTO();
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error fetching mod details: {ex.Message}", ex);
                return null;
            }
        }

        public async Task<ModDetailsFullDTO?> GetModDetailsFullAsync(string modName)
        {
            try
            {
                var url = $"{_baseUrl}/{modName}/full?version=2.0&hide_deprecated=true";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var portalResponse = JsonSerializer.Deserialize<ModDetailsFull>(json);

                if (portalResponse == null) return null;
                return portalResponse.ToFullDTO();
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error fetching mod details: {ex.Message}", ex);
                return null;
            }
        }

        public async Task<List<string>> GetRecentlyUpdatedModsAsync(int hoursAgo, bool isManual = false)
        {
            try
            {
                var sinceTime = DateTime.UtcNow.AddHours(-hoursAgo);
                _logService.LogDebug($"Started looking for updates since {sinceTime.ToLocalTime():yyyy-MM-dd HH:mm:ss} on the portal. (isManual={isManual})");
                var recentModNames = new HashSet<string>();
                var pageSize = 100;
                var maxPages = 5;
                for (int page = 1; page <= maxPages; page++)
                {
                    var url = $"{_baseUrl}?version=2.0&hide_deprecated=true&sort=updated_at&sort_order=desc&page={page}&page_size={pageSize}";
                    var response = await _httpClient.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logService.LogWarning($"API request failed: {response.StatusCode}");
                        break;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ModListResponse>(json);

                    if (result?.Results == null || result.Results.Count == 0)
                    {
                        break;
                    }

                    var foundRecentMod = false;
                    var hitLastKnown = false;

                    foreach (var mod in result.Results)
                    {
                        if (mod.LatestRelease is null) continue;

                        var releasedAt = mod.LatestRelease.ReleasedAt.ToUniversalTime();

                        // If this is an automatic check (not manual) and we encounter the last cached known update,
                        // stop scanning to avoid re-processing previously seen updates.
                        if (!isManual && _lastUpdateTime != DateTime.MinValue && releasedAt == _lastUpdateTime)
                        {
                            hitLastKnown = true;
                            break;
                        }

                        if (releasedAt >= sinceTime)
                        {
                            recentModNames.Add(mod.Name);
                            if (_lastUpdateTime < releasedAt)
                            {
                                _lastUpdateTime = releasedAt;
                                _lastModName = mod.Name;
                            }
                            foundRecentMod = true;
                        }
                    }

                    if (hitLastKnown)
                    {
                        // stop pagination early when automatic and we've reached known boundary
                        break;
                    }

                    if (!foundRecentMod)
                    {
                        break;
                    }

                    await Task.Delay(100);
                }
                if (_lastUpdateTime != DateTime.MinValue)
                {
                    _logService.LogDebug($"Latest update known is of {_lastModName} at {_lastUpdateTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}.");
                }
                _logService.LogDebug($"Found {recentModNames.Count} updates on the portal.");
                return [.. recentModNames];
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error fetching recently updated mods: {ex.Message}", ex);
                return [];
            }
        }

        public async Task DownloadModAsync(
            string downloadUrl,
            string destinationPath,
            IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logService.Log($"Starting download from {downloadUrl}");

                // Ensure directory exists
                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Get response with headers to know content length
                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;

                // Download to temporary file first
                var tempPath = $"{destinationPath}.tmp";

                await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long totalBytesRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        totalBytesRead += bytesRead;

                        // Report progress
                        progress?.Report((totalBytesRead, totalBytes));
                    }
                }

                // Move temp file to final destination (atomic operation)
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.Move(tempPath, destinationPath);

                _logService.Log($"Download completed: {Path.GetFileName(destinationPath)} ({totalBytes ?? 0:N0} bytes)");
            }
            catch (OperationCanceledException)
            {
                _logService.LogWarning($"Download cancelled: {Path.GetFileName(destinationPath)}");

                // Clean up temp file
                var tempPath = $"{destinationPath}.tmp";
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Download failed: {ex.Message}", ex);

                // Clean up temp file
                var tempPath = $"{destinationPath}.tmp";
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                throw;
            }
        }

        public void ClearCache()
        {
            // No-op for non-cached implementation
        }
    }
}