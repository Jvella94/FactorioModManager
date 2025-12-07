using FactorioModManager.Services;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowVM
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
                    LogService.Instance.Log("Checking for already-downloaded updates...");

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
                            LogService.Instance.Log($"Found already-downloaded update for {mod.Title}: {mod.LatestVersion}");

                            // Clear the update flag
                            _metadataService.ClearUpdate(mod.Name);

                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                mod.HasUpdate = false;
                                mod.LatestVersion = null;
                            });

                            clearedCount++;
                        }
                    }

                    if (clearedCount > 0)
                    {
                        LogService.Instance.Log($"Cleared update flags for {clearedCount} already-downloaded mod(s)");

                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            this.RaisePropertyChanged(nameof(ModCountSummary));
                            StatusText = $"Found {clearedCount} already-downloaded update(s)";
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogDebug($"Error checking for already-downloaded updates: {ex.Message}");
                }
            });
        }

        private async Task DownloadUpdateAsync(ModViewModel? mod)
        {
            if (mod == null || !mod.HasUpdate || string.IsNullOrEmpty(mod.LatestVersion))
            {
                return;
            }

            // Store the mod name to reselect after refresh
            var modName = mod.Name;

            await Task.Run(async () =>
            {
                try
                {
                    LogService.Instance.Log($"Starting update for {mod.Title} from {mod.Version} to {mod.LatestVersion}");

                    // Set downloading state
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        mod.IsDownloading = true;
                        mod.HasDownloadProgress = false;
                        mod.DownloadStatusText = $"Preparing download for {mod.Title}...";
                        StatusText = $"Downloading update for {mod.Title}...";
                    });

                    var apiKey = _settingsService.GetApiKey();
                    var modDetails = await _apiService.GetModDetailsAsync(mod.Name, apiKey);

                    if (modDetails?.Releases == null)
                    {
                        LogService.Instance.Log($"Failed to fetch release details for {mod.Name}");
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
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
                        LogService.Instance.Log($"No download URL found for {mod.Name}");
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            mod.IsDownloading = false;
                            StatusText = $"No download URL available for {mod.Title}";
                        });
                        return;
                    }

                    // Get username and token for download authentication
                    var username = _settingsService.GetUsername();
                    var token = _settingsService.GetToken();

                    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
                    {
                        LogService.Instance.Log("Download requires username and token from Factorio");
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            mod.IsDownloading = false;
                            StatusText = $"Cannot download {mod.Title}: Missing Factorio credentials. Please check Settings.";
                        });
                        return;
                    }

                    // Build download URL with authentication
                    var downloadUrl = $"https://mods.factorio.com{latestRelease.DownloadUrl}?username={Uri.EscapeDataString(username)}&token={Uri.EscapeDataString(token)}";
                    var modsDirectory = ModPathHelper.GetModsDirectory();
                    var newFileName = $"{mod.Name}_{latestRelease.Version}.zip";
                    var newFilePath = Path.Combine(modsDirectory, newFileName);

                    // Download file with progress reporting
                    using (var httpClient = new HttpClient())
                    {
                        LogService.Instance.Log($"Downloading from {downloadUrl.Replace(token, "***")}");

                        using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                        if (!response.IsSuccessStatusCode)
                        {
                            LogService.Instance.Log($"Download failed: {response.StatusCode}");
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                mod.IsDownloading = false;
                                StatusText = $"Download failed for {mod.Title}: {response.StatusCode}";
                            });
                            return;
                        }

                        var totalBytes = response.Content.Headers.ContentLength ?? -1;

                        using var contentStream = await response.Content.ReadAsStreamAsync();
                        using var fileStream = new FileStream(newFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                var progressPercent = (double)totalRead / totalBytes * 100;
                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    mod.HasDownloadProgress = true;
                                    mod.DownloadProgress = progressPercent;
                                    mod.DownloadStatusText = $"Downloading... {progressPercent:F0}%";
                                });
                            }
                            else
                            {
                                // Indeterminate progress
                                var mbDownloaded = totalRead / 1024.0 / 1024.0;
                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    mod.DownloadStatusText = $"Downloading... {mbDownloaded:F2} MB";
                                });
                            }
                        }
                    }

                    // Update UI after download completes
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        mod.DownloadStatusText = "Verifying download...";
                        StatusText = $"Verifying download for {mod.Title}...";
                    });

                    // Verify the ZIP file is valid
                    try
                    {
                        using var archive = System.IO.Compression.ZipFile.OpenRead(newFilePath);

                        if (archive.Entries.Count == 0)
                        {
                            LogService.Instance.Log($"Downloaded ZIP file is empty for {mod.Name}");
                            File.Delete(newFilePath);
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                mod.IsDownloading = false;
                                StatusText = $"Downloaded file is corrupted for {mod.Title}";
                            });
                            return;
                        }

                        var infoEntry = archive.Entries.FirstOrDefault(e =>
                            e.FullName.EndsWith("info.json", StringComparison.OrdinalIgnoreCase));

                        if (infoEntry == null)
                        {
                            LogService.Instance.Log($"Downloaded ZIP is missing info.json for {mod.Name}");
                            File.Delete(newFilePath);
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                mod.IsDownloading = false;
                                StatusText = $"Downloaded file is invalid for {mod.Title}";
                            });
                            return;
                        }

                        LogService.Instance.Log($"ZIP file verified: {archive.Entries.Count} entries found");
                    }
                    catch (InvalidDataException ex)
                    {
                        LogService.Instance.Log($"Downloaded file is not a valid ZIP: {ex.Message}");
                        if (File.Exists(newFilePath))
                        {
                            File.Delete(newFilePath);
                        }
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            mod.IsDownloading = false;
                            StatusText = $"Downloaded file is corrupted for {mod.Title}";
                        });
                        return;
                    }

                    LogService.Instance.Log($"Successfully downloaded and verified {newFilePath}");

                    var keepOldFiles = _settingsService.GetKeepOldModFiles();
                    if (keepOldFiles == false)
                    {
                        // Delete old version
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            mod.DownloadStatusText = "Removing old version...";
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

                    // Clear the update flag
                    _metadataService.UpdateLatestVersion(mod.Name, latestRelease.Version, hasUpdate: false);

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        mod.IsDownloading = false;
                        mod.DownloadStatusText = "Update complete!";
                        StatusText = $"Update complete for {mod.Title}. Refreshing...";
                    });

                    // Refresh mods list
                    await Task.Delay(500);
                    await RefreshModsAsync();

                    // Reselect the updated mod
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
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
                            LogService.Instance.Log($"Reselected updated mod: {updatedMod.Title}");
                        }
                        else
                        {
                            StatusText = $"Update complete but could not find mod {modName}";
                            LogService.Instance.Log($"Warning: Could not find mod {modName} after refresh");
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogService.Instance.Log($"Error updating {mod?.Title}: {ex.Message}");
                    LogService.LogDebug($"Update error details: {ex}");
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
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

        internal async Task CheckForUpdatesAsync(string? apiKey, int hoursAgo = 1)
        {
            LogService.Instance.Log($"Checking for updates from the last {hoursAgo} hour(s)...");

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = "Fetching recently updated mods...";
            });

            try
            {
                var recentlyUpdatedModNames = await _apiService.GetRecentlyUpdatedModsAsync(hoursAgo, apiKey);
                LogService.LogDebug($"Found {recentlyUpdatedModNames.Count} recently updated mods on portal");

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

                        // Update summary
                        this.RaisePropertyChanged(nameof(ModCountSummary));
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

        private async Task CheckSingleModUpdateAsync()
        {
            if (SelectedMod == null) return;

            await Task.Run(async () =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    StatusText = $"Checking for update: {SelectedMod.Title}...";
                });

                try
                {
                    var apiKey = _settingsService.GetApiKey();
                    var details = await _apiService.GetModDetailsAsync(SelectedMod.Name, apiKey);

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

                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
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
                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
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
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            StatusText = $"No release information found for {SelectedMod.Title}";
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogDebug($"Error checking update for {SelectedMod.Name}: {ex.Message}");
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Error checking update for {SelectedMod.Title}";
                    });
                }
            });
        }

    }
}
