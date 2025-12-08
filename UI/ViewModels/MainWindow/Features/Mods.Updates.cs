using FactorioModManager.Models;
using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

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

                    var modsDirectory = ModPathHelper.GetModsDirectory();
                    var modsWithUpdates = Mods.Where(m => m.HasUpdate && !string.IsNullOrEmpty(m.LatestVersion)).ToList();

                    if (modsWithUpdates.Count == 0)
                    {
                        return;
                    }

                    var clearedCount = 0;

                    foreach (var mod in modsWithUpdates)
                    {
                        // Check if the latest version file already exists
                        var latestVersionFileName = $"{mod.Name}_{mod.LatestVersion}.zip";
                        var latestVersionPath = Path.Combine(modsDirectory, latestVersionFileName);

                        if (File.Exists(latestVersionPath))
                        {
                            // Update was already downloaded externally
                            _logService.Log($"Found already-downloaded update for {mod.Title}: {mod.LatestVersion}");

                            // Clear the update flag
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
                            StatusText = $"Found {clearedCount} already-downloaded update(s)";
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogDebug($"Error checking for already-downloaded updates: {ex.Message}");
                }
            });
        }

        private bool CheckMandatoryDependencies(ModViewModel mod)
        {
            var missingDeps = new List<string>();

            foreach (var dep in mod.Dependencies)
            {
                // Skip optional dependencies (start with ?)
                if (dep.TrimStart().StartsWith('?') || dep.Contains("(?)"))
                    continue;

                // Skip game dependencies
                var gameDeps = new[] { "base", "space-age", "quality", "elevated-rails" };
                var depName = dep.Split([' ', '>', '<', '=', '!', '(', ')'], StringSplitOptions.RemoveEmptyEntries)[0];

                if (gameDeps.Contains(depName, StringComparer.OrdinalIgnoreCase))
                    continue;

                // Check if dependency is installed
                if (!Mods.Any(m => m.Name.Equals(depName, StringComparison.OrdinalIgnoreCase)))
                {
                    missingDeps.Add(depName);
                }
            }

            if (missingDeps.Count > 0)
            {
                _uiService.Post(() =>
                {
                    StatusText = $"Missing dependencies for {mod.Title}: {string.Join(", ", missingDeps)}";
                });
                _logService.LogWarning($"Cannot update {mod.Title}: Missing mandatory dependencies: {string.Join(", ", missingDeps)}");
                return false;
            }

            return true;
        }


        private async Task DownloadUpdateAsync(ModViewModel? mod)
        {
            if (mod == null || !mod.HasUpdate || string.IsNullOrEmpty(mod.LatestVersion))
            {
                return;
            }

            // Check dependencies first
            if (!CheckMandatoryDependencies(mod))
            {
                return;
            }

            var modName = mod.Name;

            await Task.Run(async () =>
            {
                try
                {
                    _logService.Log($"Starting update for {mod.Title} from {mod.Version} to {mod.LatestVersion}");

                    // Set downloading state
                    await _uiService.InvokeAsync(() =>
                    {
                        mod.IsDownloading = true;
                        mod.HasDownloadProgress = false;
                        mod.DownloadStatusText = $"Preparing download for {mod.Title}...";
                        StatusText = $"Downloading update for {mod.Title}...";
                    });

                    var apiKey = _settingsService.GetApiKey();
                    var modDetails = await _apiService.GetModDetailsAsync(mod.Name);

                    if (modDetails?.Releases == null)
                    {
                        _logService.Log($"Failed to fetch release details for {mod.Name}", LogLevel.Error);
                        _uiService.Post(() =>
                        {
                            mod.IsDownloading = false;
                            StatusText = $"Failed to fetch update details for {mod.Title}";
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
                            StatusText = $"No download URL available for {mod.Title}";
                        });
                        return;
                    }

                    // Use the common download method with progress reporting
                    var result = await DownloadModFromPortalAsync(
                        mod.Name,
                        mod.Title,
                        latestRelease.Version,
                        latestRelease.DownloadUrl,
                        mod  // Pass mod for progress reporting
                    );

                    if (!result.Success || !result.Value)
                    {
                        _uiService.Post(() =>
                        {
                            mod.IsDownloading = false;
                        });
                        return;
                    }

                    // Delete old versions if setting is enabled
                    var modsDirectory = ModPathHelper.GetModsDirectory();
                    var newFilePath = Path.Combine(modsDirectory, $"{mod.Name}_{latestRelease.Version}.zip");
                    DeleteOldModVersions(mod.Name, newFilePath);

                    _logService.Log($"Successfully updated {mod.Title} to version {latestRelease.Version}");

                    // Clear the update flag
                    _metadataService.UpdateLatestVersion(mod.Name, latestRelease.Version, hasUpdate: false);

                    _uiService.Post(() =>
                    {
                        mod.IsDownloading = false;
                        mod.DownloadStatusText = "Update complete!";
                        StatusText = $"Update complete for {mod.Title}. Refreshing...";
                    });

                    // Refresh mods list
                    await Task.Delay(500);
                    await RefreshModsAsync();

                    // Reselect the updated mod
                    await _uiService.InvokeAsync(() =>
                    {
                        var updatedMod = Mods.FirstOrDefault(m => m.Name == modName);
                        if (updatedMod != null)
                        {
                            updatedMod.SelectedVersion = updatedMod.Version;
                            SelectedMod = updatedMod;

                            if (!FilteredMods.Contains(updatedMod))
                            {
                                UpdateFilteredMods();
                            }

                            StatusText = $"Successfully updated {updatedMod.Title} to {updatedMod.Version}";
                            _logService.Log($"Reselected updated mod: {updatedMod.Title}");
                        }
                        else
                        {
                            StatusText = $"Update complete but could not find mod {modName}";
                            _logService.Log($"Warning: Could not find mod {modName} after refresh", LogLevel.Warning);
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logService.Log($"Error updating {mod?.Title}: {ex.Message}", LogLevel.Error);
                    _logService.LogDebug($"Update error details: {ex}");
                    _uiService.Post(() =>
                    {
                        if (mod != null)
                        {
                            mod.IsDownloading = false;
                            mod.DownloadStatusText = $"Error: {ex.Message}";
                        }
                        StatusText = $"Error updating {mod?.Title}: {ex.Message}";
                    });
                }
            });
        }

        internal async Task CheckForUpdatesAsync(int hoursAgo = 1)
        {
            _logService.Log($"Checking for updates from the last {hoursAgo} hour(s)...");

            _uiService.Post(() =>
            {
                StatusText = "Fetching recently updated mods...";
            });

            try
            {
                var recentlyUpdatedModNames = await _apiService.GetRecentlyUpdatedModsAsync(hoursAgo);
                _logService.LogDebug($"Found {recentlyUpdatedModNames.Count} recently updated mods on portal");

                if (recentlyUpdatedModNames.Count == 0)
                {
                    _uiService.Post(() =>
                    {
                        StatusText = "No recently updated mods found";
                    });
                    return;
                }

                var modsSnapshot = Mods.ToList();
                var installedRecentlyUpdated = modsSnapshot
                    .Where(m => recentlyUpdatedModNames.Contains(m.Name))
                    .ToList();

                _logService.Log($"Checking {installedRecentlyUpdated.Count} of your installed mods for updates");

                var updateCount = 0;
                var currentIndex = 0;

                foreach (var mod in installedRecentlyUpdated)
                {
                    currentIndex++;

                    _uiService.Post(() =>
                    {
                        StatusText = $"Checking updates ({currentIndex}/{installedRecentlyUpdated.Count}): {mod.Title}";
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

                                if (CompareVersions(latestVersion, mod.Version) > 0)
                                {
                                    _metadataService.UpdateLatestVersion(mod.Name, latestVersion, hasUpdate: true);

                                    _uiService.Post(() =>
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
                        _logService.LogDebug($"Error checking updates for {mod.Name}: {ex.Message}");
                    }
                }

                _uiService.Post(() =>
                {
                    if (updateCount > 0)
                    {
                        StatusText = $"Found {updateCount} mod update(s) available";
                        _logService.Log($"Update check complete: {updateCount} updates found");

                        // Update summary
                        this.RaisePropertyChanged(nameof(ModCountSummary));
                    }
                    else
                    {
                        StatusText = "All mods are up to date";
                        _logService.Log("All mods are up to date");
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.Log($"Error during update check: {ex.Message}");
                _logService.LogDebug($"Error in CheckForUpdatesAsync: {ex}");
            }
        }

        private async Task CheckSingleModUpdateAsync()
        {
            if (SelectedMod == null) return;

            await Task.Run(async () =>
            {
                _uiService.Post(() =>
                {
                    StatusText = $"Checking for update: {SelectedMod.Title}...";
                });

                try
                {
                    var apiKey = _settingsService.GetApiKey();
                    var details = await _apiService.GetModDetailsAsync(SelectedMod.Name);

                    if (details?.Releases != null && details.Releases.Count > 0)
                    {
                        var latestRelease = details.Releases
                            .OrderByDescending(r => r.ReleasedAt)
                            .FirstOrDefault();

                        if (latestRelease != null)
                        {
                            var latestVersion = latestRelease.Version;

                            if (CompareVersions(latestVersion, SelectedMod.Version) > 0)
                            {
                                _metadataService.UpdateLatestVersion(SelectedMod.Name, latestVersion, hasUpdate: true);

                                _uiService.Post(() =>
                                {
                                    SelectedMod.HasUpdate = true;
                                    SelectedMod.LatestVersion = latestVersion;
                                    this.RaisePropertyChanged(nameof(ModCountSummary));
                                    StatusText = $"Update available for {SelectedMod.Title}: {SelectedMod.Version} → {latestVersion}";
                                });
                            }
                            else
                            {
                                _metadataService.UpdateLatestVersion(SelectedMod.Name, latestVersion, hasUpdate: false);
                                _uiService.Post(() =>
                                {
                                    SelectedMod.HasUpdate = false;
                                    SelectedMod.LatestVersion = null;
                                    this.RaisePropertyChanged(nameof(ModCountSummary));
                                    StatusText = $"{SelectedMod.Title} is up to date (version {SelectedMod.Version})";
                                });
                            }
                        }
                    }
                    else
                    {
                        _uiService.Post(() =>
                        {
                            StatusText = $"No release information found for {SelectedMod.Title}";
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogDebug($"Error checking update for {SelectedMod.Name}: {ex.Message}");
                    _uiService.Post(() =>
                    {
                        StatusText = $"Error checking update for {SelectedMod.Title}";
                    });
                }
            });
        }

    }
}
