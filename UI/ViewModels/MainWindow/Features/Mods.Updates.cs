using FactorioModManager.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static FactorioModManager.Constants;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        /// <summary>
        /// Checks if mods marked as having updates already have the latest version downloaded
        /// </summary>
        internal async Task CheckForAlreadyDownloadedUpdatesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    _logService.Log("Checking for already-downloaded updates...");
                    var modsDirectory = _modService.GetModsDirectory();

                    var modsWithUpdates = _allMods
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
                            _metadataService.ClearUpdate(mod.Name);
                            _uiService.Post(() =>
                            {
                                mod.HasUpdate = false;
                                mod.LatestVersion = null;
                            });
                            clearedCount++;
                        }
                    }

                    if (clearedCount > 0)
                    {
                        _logService.Log($"Cleared update flags for {clearedCount} already-downloaded mod(s)");
                        _uiService.Post(() =>
                        {
                            this.RaisePropertyChanged(nameof(ModCountSummary));
                            SetStatus($"Found {clearedCount} already-downloaded update(s)");
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error checking for already-downloaded updates: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Checks if all mandatory dependencies are installed
        /// </summary>
        private bool CheckMandatoryDependencies(ModViewModel mod)
        {
            var mandatoryDeps = DependencyHelper.GetMandatoryDependencies(mod.Dependencies);
            var missingDeps = new List<string>();

            foreach (var depName in mandatoryDeps)
            {
                if (!_allMods.Any(m => m.Name.Equals(depName, StringComparison.OrdinalIgnoreCase)))
                {
                    missingDeps.Add(depName);
                }
            }

            if (missingDeps.Count > 0)
            {
                var message = $"Missing dependencies for {mod.Title}: {string.Join(", ", missingDeps)}";
                SetStatus(message, LogLevel.Warning);
                _logService.LogWarning($"Cannot update {mod.Title}: Missing mandatory dependencies: {string.Join(", ", missingDeps)}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Downloads an update for a mod
        /// </summary>
        private async Task DownloadUpdateAsync(ModViewModel? mod)
        {
            if (mod == null || !mod.HasUpdate || string.IsNullOrEmpty(mod.LatestVersion))
                return;

            if (!CheckMandatoryDependencies(mod))
                return;

            var modName = mod.Name;
            await Task.Run(async () =>
            {
                try
                {
                    _logService.Log($"Starting update for {mod.Title} from {mod.Version} to {mod.LatestVersion}");

                    await _uiService.InvokeAsync(() =>
                    {
                        mod.IsDownloading = true;
                        mod.HasDownloadProgress = false;
                        mod.DownloadStatusText = $"Preparing download for {mod.Title}...";
                        SetStatus($"Downloading update for {mod.Title}...");
                    });

                    var modDetails = await _apiService.GetModDetailsAsync(mod.Name);
                    if (modDetails?.Releases == null)
                    {
                        _logService.Log($"Failed to fetch release details for {mod.Name}", LogLevel.Error);
                        _uiService.Post(() =>
                        {
                            mod.IsDownloading = false;
                            SetStatus($"Failed to fetch update details for {mod.Title}", LogLevel.Error);
                        });
                        return;
                    }

                    var latestRelease = modDetails.Releases
                        .OrderByDescending(r => r.ReleasedAt)
                        .FirstOrDefault();

                    if (latestRelease == null || string.IsNullOrEmpty(latestRelease.DownloadUrl))
                    {
                        _logService.Log($"No download URL found for {mod.Name}", LogLevel.Error);
                        _uiService.Post(() =>
                        {
                            mod.IsDownloading = false;
                            SetStatus($"No download URL available for {mod.Title}", LogLevel.Error);
                        });
                        return;
                    }

                    var result = await DownloadModFromPortalAsync(
                        mod.Name,
                        mod.Title,
                        latestRelease.Version,
                        latestRelease.DownloadUrl,
                        mod
                    );

                    if (!result.Success || !result.Value)
                    {
                        _uiService.Post(() =>
                        {
                            mod.IsDownloading = false;
                        });
                        return;
                    }

                    var modsDirectory = _modService.GetModsDirectory();
                    var newFilePath = Path.Combine(modsDirectory, $"{mod.Name}_{latestRelease.Version}.zip");
                    _downloadService.DeleteOldVersions(mod.Name, newFilePath);

                    _logService.Log($"Successfully updated {mod.Title} to version {latestRelease.Version}");
                    _metadataService.UpdateLatestVersion(mod.Name, latestRelease.Version, hasUpdate: false);

                    _uiService.Post(() =>
                    {
                        mod.IsDownloading = false;
                        mod.DownloadStatusText = "Update complete!";
                        SetStatus($"Update complete for {mod.Title}. Refreshing...");
                    });

                    await Task.Delay(500);
                    await RefreshModsAsync();

                    await _uiService.InvokeAsync(() =>
                    {
                        var updatedMod = _allMods.FirstOrDefault(m => m.Name == modName);
                        if (updatedMod != null)
                        {
                            updatedMod.SelectedVersion = updatedMod.Version;
                            SelectedMod = updatedMod;
                            SetStatus($"Successfully updated {updatedMod.Title} to {updatedMod.Version}");
                            _logService.Log($"Reselected updated mod: {updatedMod.Title}");
                        }
                        else
                        {
                            SetStatus($"Update complete but could not find mod {modName}", LogLevel.Warning);
                            _logService.Log($"Warning: Could not find mod {modName} after refresh", LogLevel.Warning);
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logService.Log($"Error updating {mod?.Title}: {ex.Message}", LogLevel.Error);
                    _logService.LogError($"Update error details: {ex.Message}", ex);
                    _uiService.Post(() =>
                    {
                        if (mod != null)
                        {
                            mod.IsDownloading = false;
                            mod.DownloadStatusText = $"Error: {ex.Message}";
                        }
                        SetStatus($"Error updating {mod?.Title}: {ex.Message}", LogLevel.Error);
                    });
                }
            });
        }

        /// <summary>
        /// Checks for mod updates from the portal
        /// </summary>
        internal async Task CheckForUpdatesAsync(int hoursAgo)
        {
            _logService.Log($"Checking for updates from the last {hoursAgo} hour(s)...");
            await _uiService.InvokeAsync(() =>
            {
                SetStatus("Fetching recently updated mods...");
            });

            try
            {
                var recentlyUpdatedModNames = await _apiService.GetRecentlyUpdatedModsAsync(hoursAgo);
                if (recentlyUpdatedModNames.Count == 0)
                {
                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatus("No recently updated mods found.");
                    });
                    return;
                }

                var modsSnapshot = _allMods.ToList();
                var installedRecentlyUpdated = modsSnapshot
                    .Where(m => recentlyUpdatedModNames.Contains(m.Name))
                    .ToList();

                _logService.Log($"{installedRecentlyUpdated.Count} of your installed mods have new updates.");

                var updateCount = 0;
                var currentIndex = 0;

                foreach (var mod in installedRecentlyUpdated)
                {
                    currentIndex++;
                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatus($"Checking updates for ({currentIndex}/{installedRecentlyUpdated.Count}): {mod.Title}");
                    });

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
                                if (VersionHelper.IsNewerVersion(latestVersion, mod.Version))
                                {
                                    _metadataService.UpdateLatestVersion(mod.Name, latestVersion, hasUpdate: true);
                                    await _uiService.InvokeAsync(() =>
                                    {
                                        mod.HasUpdate = true;
                                        mod.LatestVersion = latestVersion;
                                    });
                                    updateCount++;
                                    _logService.Log($"Update available for {mod.Title}: {mod.Version} → {latestVersion}");
                                }
                                else
                                {
                                    _metadataService.UpdateLatestVersion(mod.Name, latestVersion, hasUpdate: false);
                                }
                            }
                        }

                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"Error checking updates for {mod.Name}: {ex.Message}", ex);
                    }
                }

                await _uiService.InvokeAsync(() =>
                {
                    if (updateCount > 0)
                    {
                        SetStatus($"Found {updateCount} mod update(s) available.");
                        this.RaisePropertyChanged(nameof(ModCountSummary));
                    }
                    else
                    {
                        SetStatus("All mods are up to date.");
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.Log($"Error during update check: {ex.Message}", LogLevel.Error);
                _logService.LogError($"Error in CheckForUpdatesAsync: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks for updates for the currently selected mod
        /// </summary>
        private async Task CheckSingleModUpdateAsync()
        {
            if (SelectedMod == null)
            {
                SetStatus("No mod selected", LogLevel.Warning);
                return;
            }

            await Task.Run(async () =>
            {
                await _uiService.InvokeAsync(() =>
                {
                    SetStatus($"Checking for update: {SelectedMod.Title}...");
                });

                try
                {
                    var details = await _apiService.GetModDetailsAsync(SelectedMod.Name);
                    if (details?.Releases != null && details.Releases.Count > 0)
                    {
                        var latestRelease = details.Releases
                            .OrderByDescending(r => r.ReleasedAt)
                            .FirstOrDefault();

                        if (latestRelease != null)
                        {
                            var latestVersion = latestRelease.Version;
                            if (VersionHelper.IsNewerVersion(latestVersion, SelectedMod.Version))
                            {
                                _metadataService.UpdateLatestVersion(SelectedMod.Name, latestVersion, hasUpdate: true);
                                await _uiService.InvokeAsync(() =>
                                {
                                    SelectedMod.HasUpdate = true;
                                    SelectedMod.LatestVersion = latestVersion;
                                    this.RaisePropertyChanged(nameof(ModCountSummary));
                                    SetStatus($"Update available for {SelectedMod.Title}: {SelectedMod.Version} → {latestVersion}");
                                });
                            }
                            else
                            {
                                _metadataService.UpdateLatestVersion(SelectedMod.Name, latestVersion, hasUpdate: false);
                                await _uiService.InvokeAsync(() =>
                                {
                                    SelectedMod.HasUpdate = false;
                                    SelectedMod.LatestVersion = null;
                                    this.RaisePropertyChanged(nameof(ModCountSummary));
                                    SetStatus($"{SelectedMod.Title} is up to date (version {SelectedMod.Version})");
                                });
                            }
                        }
                    }
                    else
                    {
                        await _uiService.InvokeAsync(() =>
                        {
                            SetStatus($"No release information found for {SelectedMod.Title}", LogLevel.Warning);
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error checking update for {SelectedMod.Name}: {ex.Message}", ex);
                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatus($"Error checking update for {SelectedMod.Title}", LogLevel.Error);
                    });
                }
            });
        }
    }
}