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

        // New: check for updates and notify the user via UI service when appropriate
        Task CheckForUpdatesAndNotifyAsync(string currentVersion);
    }

    public class AppUpdateChecker(ILogService logService, HttpClient httpClient, IUIService uiService) : IAppUpdateChecker
    {
        private readonly ILogService _logService = logService;
        private readonly HttpClient _httpClient = httpClient;
        private readonly IUIService _uiService = uiService;

        public async Task<AppUpdateInfo?> CheckForUpdatesAsync(string currentVersion)
        {
            try
            {
                _logService.Log("Checking for app updates on GitHub...", LogLevel.Info);

                var url = "https://api.github.com/repos/jvella94/FactorioModManager/releases/latest";
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FactorioModManager/1.0");

                var response = await _httpClient.GetStringAsync(url);
                var release = JsonSerializer.Deserialize<GitHubRelease>(response, Constants.JsonHelper.CaseInsensitive);

                if (release?.TagName == null || release.HtmlUrl == null)
                {
                    _logService.Log("Invalid release data from GitHub", LogLevel.Warning);
                    return null;
                }

                var latestVersion = release.TagName.TrimStart('v');
                var isNewer = IsNewerVersion(latestVersion, currentVersion);

                var downloadUrl = release.Assets?
                    .FirstOrDefault(a => !string.IsNullOrEmpty(a.Name) && a.Name.Contains(".zip"))
                    ?.BrowserDownloadUrl
                    ?? release.HtmlUrl;

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

        public async Task CheckForUpdatesAndNotifyAsync(string currentVersion)
        {
            var updateInfo = await CheckForUpdatesAsync(currentVersion);
            if (updateInfo?.IsNewer == true)
            {
                await _uiService.InvokeAsync(() =>
                {
                    _uiService.ShowMessageAsync("Update Available",
                        $"A new version {updateInfo.Version} of Factorio Mod Manager is available!\n\nRelease notes: {updateInfo.HtmlUrl}");
                    _logService.Log($"New version {updateInfo.Version} available (notified user)");
                });
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