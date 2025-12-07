using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FactorioModManager.Services;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowVM
    {
        private async Task DownloadUpdateAsync(ModViewModel? mod)
        {
            if (mod == null || !mod.HasUpdate || string.IsNullOrEmpty(mod.LatestVersion))
            {
                return;
            }

            await Task.Run(async () =>
            {
                try
                {
                    LogService.Instance.Log($"Starting update for {mod.Title} from {mod.Version} to {mod.LatestVersion}");

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Downloading update for {mod.Title}...";
                    });

                    var apiKey = _settingsService.GetApiKey();
                    var modDetails = await _apiService.GetModDetailsAsync(mod.Name, apiKey);

                    if (modDetails?.Releases == null)
                    {
                        LogService.Instance.Log($"Failed to fetch release details for {mod.Name}");
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            StatusText = $"Failed to fetch update details for {mod.Title}";
                        });
                        return;
                    }

                    var latestRelease = modDetails.Releases
                        .OrderByDescending(r => r.ReleasedAt)
                        .FirstOrDefault();

                    if (latestRelease == null || string.IsNullOrEmpty(latestRelease.DownloadUrl))
                    {
                        LogService.Instance.Log($"No download URL found for {mod.Name}");
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            StatusText = $"No download URL available for {mod.Title}";
                        });
                        return;
                    }

                    // Download the mod
                    var downloadUrl = $"https://mods.factorio.com{latestRelease.DownloadUrl}";
                    var modsDirectory = ModPathHelper.GetModsDirectory();

                    var newFileName = $"{mod.Name}_{latestRelease.Version}.zip";
                    var newFilePath = Path.Combine(modsDirectory, newFileName);

                    // Download file
                    using (var httpClient = new HttpClient())
                    {
                        if (!string.IsNullOrEmpty(apiKey))
                        {
                            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                        }

                        LogService.Instance.Log($"Downloading from {downloadUrl}");
                        var response = await httpClient.GetAsync(downloadUrl);

                        if (!response.IsSuccessStatusCode)
                        {
                            LogService.Instance.Log($"Download failed: {response.StatusCode}");
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                StatusText = $"Download failed for {mod.Title}";
                            });
                            return;
                        }

                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            StatusText = $"Saving update for {mod.Title}...";
                        });

                        var content = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(newFilePath, content);
                    }

                    LogService.Instance.Log($"Downloaded to {newFilePath}");

                    var keepOldFiles = _settingsService.GetKeepOldModFiles();

                    if (keepOldFiles == false)
                    {
                        // Delete old version
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            StatusText = $"Removing old version of {mod.Title}...";
                        });

                        var oldFiles = Directory.GetFiles(modsDirectory, $"{mod.Name}_*.zip")
                            .Where(f => !f.Equals(newFilePath, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        foreach (var oldFile in oldFiles)
                        {
                            File.Delete(oldFile);
                            LogService.Instance.Log($"Deleted {Path.GetFileName(oldFile)}");
                        }
                    }

                    LogService.Instance.Log($"Successfully updated {mod.Title} to version {latestRelease.Version}");

                    // ADDED: Clear the update flag since we just updated
                    _metadataService.ClearUpdate(mod.Name);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Update complete for {mod.Title}. Refreshing...";
                    });

                    // Refresh mods list
                    await Task.Delay(1000);
                    await RefreshModsAsync();

                }
                catch (Exception ex)
                {
                    LogService.Instance.Log($"Error updating {mod?.Title}: {ex.Message}");
                    LogService.LogDebug($"Update error details: {ex}");

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Error updating {mod?.Title}: {ex.Message}";
                    });
                }
            });
        }

        private async Task CheckForUpdatesAsync(string? apiKey, int hoursAgo = 1)
        {
            LogService.Instance.Log($"Checking for updates from the last {hoursAgo} hour(s)...");

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = "Fetching recently updated mods...";
            });

            try
            {
                var recentlyUpdatedModNames = await _apiService.GetRecentlyUpdatedModsAsync(hoursAgo, apiKey);
                LogService.Instance.Log($"Found {recentlyUpdatedModNames.Count} recently updated mods on portal");

                if (recentlyUpdatedModNames.Count == 0)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = "No recently updated mods found";
                    });
                    return;
                }

                var modsSnapshot = Mods.ToList();
                var installedRecentlyUpdated = modsSnapshot
                    .Where(m => recentlyUpdatedModNames.Contains(m.Name))
                    .ToList();

                LogService.Instance.Log($"Checking {installedRecentlyUpdated.Count} of your installed mods for updates");

                var updateCount = 0;
                var currentIndex = 0;

                foreach (var mod in installedRecentlyUpdated)
                {
                    currentIndex++;

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Checking updates ({currentIndex}/{installedRecentlyUpdated.Count}): {mod.Title}";
                    });

                    try
                    {
                        var details = await _apiService.GetModDetailsAsync(mod.Name, apiKey);
                        if (details?.Releases != null && details.Releases.Count > 0)
                        {
                            var latestRelease = details.Releases
                                .OrderByDescending(r => r.ReleasedAt)
                                .FirstOrDefault();
                            if (latestRelease != null)
                            {
                                var latestVersion = latestRelease.Version;

                                if (CompareVersions(latestVersion, mod.Version) > 0)
                                {
                                    _metadataService.UpdateLatestVersion(mod.Name, latestVersion, hasUpdate: true);

                                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                    {
                                        mod.HasUpdate = true;
                                        mod.LatestVersion = latestVersion;
                                    });
                                    updateCount++;
                                    LogService.Instance.Log($"Update available for {mod.Title}: {mod.Version} → {latestVersion}");
                                }
                                else
                                {
                                    // ADDED: Clear update flag if mod is up to date
                                    _metadataService.UpdateLatestVersion(mod.Name, latestVersion, hasUpdate: false);
                                }
                            }
                        }

                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogDebug($"Error checking updates for {mod.Name}: {ex.Message}");
                    }
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (updateCount > 0)
                    {
                        StatusText = $"Found {updateCount} mod update(s) available";
                        LogService.Instance.Log($"Update check complete: {updateCount} updates found");

                        // Sort mods to show updates at top
                        SortModsWithUpdatesFirst();
                    }
                    else
                    {
                        StatusText = "All mods are up to date";
                        LogService.Instance.Log("All mods are up to date");
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Error during update check: {ex.Message}");
                LogService.LogDebug($"Error in CheckForUpdatesAsync: {ex}");
            }
        }

        private void SortModsWithUpdatesFirst()
        {
            var sorted = Mods
                .OrderByDescending(m => m.HasUpdate)
                .ThenByDescending(m => m.LastUpdated ?? DateTime.MinValue)
                .ToList();

            Mods.Clear();
            foreach (var mod in sorted)
            {
                Mods.Add(mod);
            }

            UpdateFilteredMods();
        }
    }
}
