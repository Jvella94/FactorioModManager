using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Mods;
using FactorioModManager.Services.Settings;
using FactorioModManager.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FactorioModManager.Services.API
{
    public interface IModUpdateService
    {
        Task<UpdateCheckResult> CheckForUpdatesAsync(int hoursAgo, IEnumerable<ModViewModel> installedMods);
        Task<UpdateCheckResult> CheckSingleModUpdateAsync(string modName, string currentVersion);
        Task<Result<bool>> ApplyUpdateAsync(string modName, string targetVersion);
        Task CheckForAlreadyDownloadedUpdatesAsync(IEnumerable<ModViewModel> mods);
    }

    /// <summary>
    /// Result of checking for mod updates
    /// </summary>
    public record UpdateCheckResult(
        int TotalModsChecked,
        int UpdatesAvailable,
        List<ModUpdateInfo> Updates,
        DateTime CheckedAt
    );

    /// <summary>
    /// Information about a mod update
    /// </summary>
    public record ModUpdateInfo(
        string ModName,
        string ModTitle,
        string CurrentVersion,
        string LatestVersion,
        DateTime ReleasedAt
    );

        public class ModUpdateService(
            IFactorioApiService apiService,
            IModMetadataService metadataService,
            IModPathSettings pathSettings,
            ILogService logService) : IModUpdateService
        {
            private readonly IFactorioApiService _apiService = apiService;
            private readonly IModMetadataService _metadataService = metadataService;
            private readonly IModPathSettings _pathSettings = pathSettings;
            private readonly ILogService _logService = logService;

            public async Task<UpdateCheckResult> CheckForUpdatesAsync(
                int hoursAgo,
                IEnumerable<ModViewModel> installedMods)
            {
                var sinceTime = DateTime.UtcNow.AddHours(-hoursAgo);
                _logService.LogDebug($"Checking for updates since {sinceTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}");

                var recentlyUpdatedModNames = await _apiService.GetRecentlyUpdatedModsAsync(hoursAgo);
                var updates = new List<ModUpdateInfo>();

                var modsToCheck = installedMods
                    .Where(m => recentlyUpdatedModNames.Contains(m.Name))
                    .ToList();

                _logService.Log($"{modsToCheck.Count} of your installed mods have new updates on the portal.");

                foreach (var mod in modsToCheck)
                {
                    try
                    {
                        var details = await _apiService.GetModDetailsAsync(mod.Name);
                        if (details?.Releases != null && details.Releases.Count > 0)
                        {
                            var latestRelease = details.Releases
                                .OrderByDescending(r => r.ReleasedAt)
                                .FirstOrDefault();

                            if (latestRelease != null)
                            {
                                var latestVersion = latestRelease.Version;
                                if (Constants.VersionHelper.IsNewerVersion(latestVersion, mod.Version))
                                {
                                    _metadataService.UpdateLatestVersion(mod.Name, latestVersion, hasUpdate: true);

                                    updates.Add(new ModUpdateInfo(
                                        mod.Name,
                                        mod.Title,
                                        mod.Version,
                                        latestVersion,
                                        latestRelease.ReleasedAt
                                    ));

                                    _logService.Log($"Update available for {mod.Title}: {mod.Version} → {latestVersion}");
                                }
                                else
                                {
                                    _metadataService.UpdateLatestVersion(mod.Name, latestVersion, hasUpdate: false);
                                }
                            }
                        }

                        await Task.Delay(100); // Throttle
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"Error checking updates for {mod.Name}: {ex.Message}", ex);
                    }
                }

                return new UpdateCheckResult(
                    modsToCheck.Count,
                    updates.Count,
                    updates,
                    DateTime.UtcNow
                );
            }

            public async Task<UpdateCheckResult> CheckSingleModUpdateAsync(string modName, string currentVersion)
            {
                var updates = new List<ModUpdateInfo>();

                try
                {
                    var details = await _apiService.GetModDetailsAsync(modName);
                    if (details?.Releases != null && details.Releases.Count > 0)
                    {
                        var latestRelease = details.Releases
                            .OrderByDescending(r => r.ReleasedAt)
                            .FirstOrDefault();

                        if (latestRelease != null)
                        {
                            var latestVersion = latestRelease.Version;
                            if (Constants.VersionHelper.IsNewerVersion(latestVersion, currentVersion))
                            {
                                _metadataService.UpdateLatestVersion(modName, latestVersion, hasUpdate: true);

                                updates.Add(new ModUpdateInfo(
                                    modName,
                                    details.Title,
                                    currentVersion,
                                    latestVersion,
                                    latestRelease.ReleasedAt
                                ));
                            }
                            else
                            {
                                _metadataService.UpdateLatestVersion(modName, latestVersion, hasUpdate: false);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error checking update for {modName}: {ex.Message}", ex);
                }

                return new UpdateCheckResult(
                    1,
                    updates.Count,
                    updates,
                    DateTime.UtcNow
                );
            }

            public async Task<Result<bool>> ApplyUpdateAsync(string modName, string targetVersion)
            {
                await Task.CompletedTask;
                return Result.Ok(true);
            }

            public async Task CheckForAlreadyDownloadedUpdatesAsync(IEnumerable<ModViewModel> mods)
            {
                await Task.Run(() =>
                {
                    try
                    {
                        _logService.Log("Checking for already-downloaded updates...");
                        var modsDirectory = _pathSettings.GetModsPath();

                        var modsWithUpdates = mods
                            .Where(m => m.HasUpdate && !string.IsNullOrEmpty(m.LatestVersion))
                            .ToList();

                        if (modsWithUpdates.Count == 0)
                            return;

                        var clearedCount = 0;
                        foreach (var mod in modsWithUpdates)
                        {
                            var latestVersionFileName = $"{mod.Name}_{mod.LatestVersion}.zip";
                            var latestVersionPath = Path.Combine(modsDirectory, latestVersionFileName);

                            if (File.Exists(latestVersionPath))
                            {
                                _logService.Log($"Found already-downloaded update for {mod.Title}: {mod.LatestVersion}");
                                _metadataService.ClearModUpdateInfo(mod.Name);
                                clearedCount++;
                            }
                        }

                        if (clearedCount > 0)
                        {
                            _logService.Log($"Cleared update flags for {clearedCount} already-downloaded mod(s)");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"Error checking for already-downloaded updates: {ex.Message}", ex);
                    }
                });
            }
        }
    }