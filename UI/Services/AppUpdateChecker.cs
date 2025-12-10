using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FactorioModManager.Services
{
    public class AppUpdateInfo
    {
        public string Version { get; set; } = string.Empty;
        public string? HtmlUrl { get; set; } = string.Empty;
        public string? DownloadUrl { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public bool IsNewer { get; set; }
    }

    public interface IAppUpdateChecker
    {
        Task<AppUpdateInfo?> CheckForUpdatesAsync(string currentVersion);
    }

    public class AppUpdateChecker(ILogService logService, HttpClient httpClient) : IAppUpdateChecker
    {
        private readonly ILogService _logService = logService;
        private readonly HttpClient _httpClient = httpClient;

        public async Task<AppUpdateInfo?> CheckForUpdatesAsync(string currentVersion)
        {
            try
            {
                _logService.Log("Checking for app updates on GitHub...", LogLevel.Info);

                var url = "https://api.github.com/repos/jvella94/FactorioModManager/releases/latest";
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "FactorioModManager/1.0");

                var response = await _httpClient.GetStringAsync(url);
                var release = JsonSerializer.Deserialize<GitHubRelease>(response, Constants.JsonHelper.CaseInsensitive);

                // Fix: Check for null values before proceeding
                if (release?.TagName == null || release.HtmlUrl == null)
                {
                    _logService.Log("Invalid release data from GitHub", LogLevel.Warning);
                    return null;
                }

                var latestVersion = release.TagName.TrimStart('v');
                var isNewer = IsNewerVersion(latestVersion, currentVersion);

                // Fix: Find asset with null-safe checks
                var downloadUrl = release.Assets?
                    .FirstOrDefault(a => !string.IsNullOrEmpty(a.Name) && a.Name.Contains(".zip"))
                    ?.BrowserDownloadUrl
                    ?? release.HtmlUrl; // Now safe because we checked it's not null above

                return new AppUpdateInfo
                {
                    Version = latestVersion,
                    HtmlUrl = release.HtmlUrl,
                    DownloadUrl = downloadUrl,
                    PublishedAt = release.PublishedAt,
                    IsNewer = isNewer
                };
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to check for app updates", ex);
                return null;
            }
        }

        private static bool IsNewerVersion(string latest, string current)
        {
            try
            {
                var latestParts = latest.Split('.').Select(int.Parse).ToArray();
                var currentParts = current.Split('.').Select(int.Parse).ToArray();

                for (int i = 0; i < Math.Max(latestParts.Length, currentParts.Length); i++)
                {
                    var l = i < latestParts.Length ? latestParts[i] : 0;
                    var c = i < currentParts.Length ? currentParts[i] : 0;
                    if (l > c) return true;
                    if (l < c) return false;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private class GitHubRelease
        {
            public string? TagName { get; set; }
            public string? HtmlUrl { get; set; }
            public DateTime PublishedAt { get; set; }
            public Asset[]? Assets { get; set; }
        }

        private class Asset
        {
            public string? Name { get; set; }
            public string? BrowserDownloadUrl { get; set; }
        }
    }
}