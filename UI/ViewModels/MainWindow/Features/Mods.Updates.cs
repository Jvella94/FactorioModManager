using FactorioModManager.Models;
using FactorioModManager.Services;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static FactorioModManager.Constants;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        // Progress properties for Update All
        private bool _isUpdatingAll;

        private int _updateAllTotal;
        private int _updateAllCompleted;
        private string? _updateAllProgressText;
        private double _updateAllProgressPercent;

        public bool IsUpdatingAll
        {
            get => _isUpdatingAll;
            set => this.RaiseAndSetIfChanged(ref _isUpdatingAll, value);
        }

        public int UpdateAllTotal
        {
            get => _updateAllTotal;
            set
            {
                this.RaiseAndSetIfChanged(ref _updateAllTotal, value);
                UpdateProgressText();
            }
        }

        public int UpdateAllCompleted
        {
            get => _updateAllCompleted;
            set
            {
                this.RaiseAndSetIfChanged(ref _updateAllCompleted, value);
                UpdateProgressText();
            }
        }

        public string? UpdateAllProgressText
        {
            get => _updateAllProgressText;
            private set => this.RaiseAndSetIfChanged(ref _updateAllProgressText, value);
        }

        public double UpdateAllProgressPercent
        {
            get => _updateAllProgressPercent;
            set => this.RaiseAndSetIfChanged(ref _updateAllProgressPercent, value);
        }

        private void UpdateProgressText()
        {
            UpdateAllProgressText = UpdateAllTotal > 0
                ? $"{UpdateAllCompleted}/{UpdateAllTotal}"
                : string.Empty;

            UpdateAllProgressPercent = UpdateAllTotal > 0
                ? (double)UpdateAllCompleted / UpdateAllTotal * 100.0
                : 0.0;
        }

        /// <summary>
        /// Update all mods that currently have pending updates. Processes mods with limited concurrency
        /// and will attempt to auto-install missing dependencies if user confirms.
        /// </summary>
        private async Task UpdateAllAsync()
        {
            var modsToUpdate = _allMods
                .Where(m => m.HasUpdate && !string.IsNullOrEmpty(m.LatestVersion))
                .ToList();

            if (modsToUpdate.Count == 0)
            {
                await _uiService.InvokeAsync(() => SetStatus("No pending updates to apply."));
                return;
            }

            // Confirm with user
            var confirmAll = await _uiService.ShowConfirmationAsync(
                "Update All Mods",
                $"This will download and install updates for {modsToUpdate.Count} mod(s). Continue?",
                null,
                "Yes",
                "No");

            if (!confirmAll)
            {
                _logService.LogDebug("UpdateAll cancelled by user");
                await _uiService.InvokeAsync(() => SetStatus("Update cancelled."));
                return;
            }

            // ------------------
            // Step 1: aggregate missing dependencies across all mods
            // ------------------
            var aggregatedMissing = new Dictionary<string, (string? Op, string? Version)>(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in modsToUpdate)
            {
                var mandatory = DependencyHelper.GetMandatoryDependencies(mod.Dependencies);
                foreach (var depRaw in mandatory)
                {
                    if (DependencyHelper.IsGameDependency(depRaw))
                        continue;

                    // Parse potential constraint
                    var parsed = DependencyHelper.ParseDependency(depRaw);
                    if (parsed == null) continue;

                    var depName = parsed.Value.Name;
                    var op = parsed.Value.VersionOperator;
                    var ver = parsed.Value.Version;

                    // Check if already installed and satisfies constraint
                    var installed = FindModByName(depName);
                    if (installed != null)
                    {
                        // If there's a constraint, validate installed version
                        if (!string.IsNullOrEmpty(op) && !string.IsNullOrEmpty(ver))
                        {
                            if (!DependencyHelper.SatisfiesVersionConstraint(installed.Version, op, ver))
                            {
                                // Installed version doesn't satisfy; mark as missing with constraint
                                aggregatedMissing[depName] = (op, ver);
                            }
                        }
                        // else installed and no constraint -> OK
                    }
                    else
                    {
                        // Not installed; add required constraint if any
                        if (!aggregatedMissing.TryGetValue(depName, out (string? Op, string? Version) existing))
                            aggregatedMissing[depName] = (op, ver);
                        else
                        {
                            if (existing.Op == null && op != null)
                                aggregatedMissing[depName] = (op, ver);
                        }
                    }
                }
            }

            // If there are aggregated missing dependencies, prompt once
            if (aggregatedMissing.Count > 0)
            {
                var listText = string.Join(", ", aggregatedMissing.Select(kv => kv.Key + (kv.Value.Op != null && kv.Value.Version != null ? $" {kv.Value.Op} {kv.Value.Version}" : string.Empty)));
                var confirmDeps = await _uiService.ShowConfirmationAsync(
                    "Install Missing Dependencies",
                    $"The following mandatory dependencies are missing or have unsatisfied version constraints: {listText}.\n\nAttempt to download and install these dependencies now?",
                    null,
                    "Install",
                    "Skip");

                if (!confirmDeps)
                {
                    await _uiService.InvokeAsync(() => SetStatus("Update cancelled: missing dependencies not installed", LogLevel.Warning));
                    return;
                }

                // Install aggregated dependencies sequentially
                foreach (var kv in aggregatedMissing)
                {
                    var depName = kv.Key;
                    // double-check not installed (it might have been installed earlier in this loop)
                    if (FindModByName(depName) != null)
                        continue;

                    // Call existing InstallMod
                    var installResult = await InstallMod(depName);
                    if (!installResult.Success)
                    {
                        _logService.LogWarning($"Failed to install aggregated dependency {depName}: {installResult.Error}");
                        await _uiService.InvokeAsync(() => SetStatus($"Failed to install dependency {depName}: {installResult.Error}", LogLevel.Warning));
                        // Abort the whole Update All - user opted to install but a dependency failed
                        return;
                    }

                    // small delay
                    await Task.Delay(200);
                }

                // Refresh mods so parallel updates see the new installs
                await RefreshModsAsync();
            }

            // ------------------
            // Step 2: run parallel updates with concurrency control
            // ------------------
            // Initialize progress
            UpdateAllTotal = modsToUpdate.Count;
            UpdateAllCompleted = 0;
            IsUpdatingAll = true;

            await _uiService.InvokeAsync(() => SetStatus($"Starting update of {modsToUpdate.Count} mod(s)..."));

            var concurrency = _settingsService.GetUpdateConcurrency();
            if (concurrency <= 0) concurrency = 3;
            var semaphore = new SemaphoreSlim(concurrency);
            var tasks = new List<Task>();
            var results = new ConcurrentBag<(string Mod, bool Success, string Message)>();

            foreach (var mod in modsToUpdate)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await _uiService.InvokeAsync(() => SetStatus($"Updating {mod.Title}..."));

                        // Ensure dependencies still satisfied (version-aware)
                        var mandatory = DependencyHelper.GetMandatoryDependencies(mod.Dependencies);
                        var missingNow = new List<string>();
                        foreach (var depRaw in mandatory)
                        {
                            if (DependencyHelper.IsGameDependency(depRaw))
                                continue;

                            var parsed = DependencyHelper.ParseDependency(depRaw);
                            if (parsed == null) continue;

                            var depName = parsed.Value.Name;
                            var op = parsed.Value.VersionOperator;
                            var ver = parsed.Value.Version;

                            var installed = FindModByName(depName);
                            if (installed == null)
                            {
                                missingNow.Add(depName);
                                break;
                            }

                            if (!DependencyHelper.SatisfiesVersionConstraint(installed.Version, op, ver))
                            {
                                missingNow.Add(depName);
                                break;
                            }
                        }

                        if (missingNow.Count > 0)
                        {
                            results.Add((mod.Title, false, "Skipped - missing/unsatisfied dependencies after install stage"));
                            await _uiService.InvokeAsync(() => SetStatus($"Skipping {mod.Title}: missing/unsatisfied dependencies", LogLevel.Warning));
                            return;
                        }

                        try
                        {
                            await DownloadUpdateAsync(mod);
                            results.Add((mod.Title, true, "Updated"));
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError($"Error updating {mod.Name} in UpdateAll parallel task: {ex.Message}", ex);
                            await _uiService.InvokeAsync(() => SetStatus($"Error updating {mod.Title}: {ex.Message}", LogLevel.Error));
                            results.Add((mod.Title, false, $"Error: {ex.Message}"));
                        }
                    }
                    finally
                    {
                        var newVal = Interlocked.Increment(ref _updateAllCompleted);
                        // Update property on UI thread so bindings update
                        await _uiService.InvokeAsync(() => UpdateAllCompleted = newVal);
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Finish progress
            IsUpdatingAll = false;

            await _uiService.InvokeAsync(() =>
            {
                SetStatus("Update all complete. Preparing summary...");
                this.RaisePropertyChanged(nameof(ModCountSummary));
            });

            // Build summary message
            var resultList = results.ToList();
            var successCount = resultList.Count(r => r.Success);
            var failedCount = resultList.Count - successCount;

            var summary = $"Update All finished. {successCount} succeeded, {failedCount} failed or skipped." + Environment.NewLine + Environment.NewLine;
            foreach (var (Mod, Success, Message) in resultList.OrderByDescending(r => r.Success))
            {
                summary += $"- {Mod}: {(Success ? "Success" : Message)}" + Environment.NewLine;
            }

            // Show summary dialog
            await _uiService.ShowMessageAsync("Update All Summary", summary);

            // Refresh mods once more
            await RefreshModsAsync();

            // Reset counters
            UpdateAllCompleted = 0;
            UpdateAllTotal = 0;
            UpdateAllProgressText = string.Empty;
        }

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
                    var modsDirectory = FolderPathHelper.GetModsDirectory();

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
                            _metadataService.ClearModUpdateInfo(mod.Name);
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
                    HandleError(ex, $"Error checking for already-downloaded updates: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Checks if all mandatory dependencies are installed
        /// </summary>
        private bool CheckMandatoryDependencies(ModViewModel mod)
        {
            var mandatoryDeps = DependencyHelper.GetMandatoryDependencies(mod.Dependencies);
            var missingDeps = ClassifyDependencies(mandatoryDeps).missing.Count > 0;
            if (missingDeps)
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

                    var modsDirectory = FolderPathHelper.GetModsDirectory();
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
        internal async Task CheckForUpdatesAsync(int hoursAgo, bool isManual = false)
        {
            _logService.Log($"Checking for updates from the last {hoursAgo} hour(s)... (isManual={isManual})");
            await _uiService.InvokeAsync(() =>
            {
                SetStatus("Fetching recently updated mods...");
            });

            try
            {
                var recentlyUpdatedModNames = await _apiService.GetRecentlyUpdatedModsAsync(hoursAgo, isManual);
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
                        HandleError(ex, $"Error checking updates for {mod.Name}: {ex.Message}");
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
                SetStatus($"Error during update check: {ex.Message}", LogLevel.Error);
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

        private ModViewModel? FindModByName(string name) =>
           _allMods.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        private sealed class InstallDependencyResolution
        {
            public bool Proceed { get; set; }
            public bool InstallEnabled { get; set; }
            public List<ModViewModel> ModsToEnable { get; } = [];
            public List<ModViewModel> ModsToDisable { get; } = [];
            public List<string> MissingDependenciesToInstall { get; } = [];
        }
    }
}