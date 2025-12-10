using FactorioModManager.Models;
using FactorioModManager.Services;
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

        /// <summary>
        /// Checks all mandatory deps for a mod about to be installed.
        /// Handles: missing deps, disabled deps, incompatible mods.
        /// Returns a resolution describing how to proceed.
        /// </summary>
        private async Task<InstallDependencyResolution> ResolveInstallDependenciesAsync(
            ModInfo modInfo)
        {
            var result = new InstallDependencyResolution
            {
                Proceed = true,
                InstallEnabled = true // default to enabled
            };

            var modTitle = modInfo.Title ?? modInfo.Name;
            var deps = modInfo.Dependencies as IReadOnlyList<string>;
            var mandatoryRaw = DependencyHelper.GetMandatoryDependencies(deps);

            var builtInRequired = mandatoryRaw
                .Where(name => DependencyHelper.GameDependencies.Contains(name, StringComparer.OrdinalIgnoreCase))
                .ToList();

            // Check official content ownership

            var missingBuiltIn = ClassifyDependencies(mandatoryRaw).missing;
            foreach (var name in builtInRequired)
            {
                switch (name.ToLowerInvariant())
                {
                    case "base":
                        // assume base is present if the game runs; no extra check
                        break;

                    case "space-age":
                    case "quality":
                    case "elevated-rails":
                        if (!_settingsService.GetHasSpaceAgeDlc())
                            missingBuiltIn.Add("Space Age DLC (includes Quality & Elevated Rails)");
                        break;
                }
            }

            if (missingBuiltIn.Count > 0)
            {
                var title = modInfo.Title ?? modInfo.Name;
                var msg =
                    $"The mod {title} requires official content that is not detected in your Factorio installation:\n\n" +
                    string.Join("\n", missingBuiltIn) +
                    "\n\nThese cannot be installed via the mod portal. " +
                    "Please enable or purchase them in Factorio itself.";

                await _uiService.ShowMessageAsync("Missing Official Content", msg);
                return new InstallDependencyResolution
                {
                    Proceed = false,
                    InstallEnabled = false
                };
            }
            // Now only consider non-built-in dependencies as portal / local mods
            var mandatoryDeps = mandatoryRaw
                .Where(d => !DependencyHelper.GameDependencies.Contains(d, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var incompatibleDeps = DependencyHelper.GetIncompatibleDependencies(deps);

            var missingDeps = new List<string>();
            var installedDisabledDeps = new List<ModViewModel>();

            // Check mandatory deps
            foreach (var depName in mandatoryDeps)
            {
                var depMod = FindModByName(depName);
                if (depMod == null)
                {
                    missingDeps.Add(depName);
                }
                else if (!depMod.IsEnabled)
                {
                    installedDisabledDeps.Add(depMod);
                }
            }

            // Check incompatible mods currently enabled
            var incompatibleLoaded = _allMods
                .Where(m => m.IsEnabled && incompatibleDeps.Contains(m.Name))
                .ToList();

            // 1) Missing deps: ask to install them
            if (missingDeps.Count > 0)
            {
                var msg =
                    $"The following mandatory dependencies for {modTitle} are not installed:\n\n" +
                    string.Join("\n", missingDeps) +
                    "\n\nDo you want to install these dependencies as well?";

                var installMissing = await _uiService.ShowConfirmationAsync(
                    "Missing Dependencies",
                    msg,
                    null);

                if (!installMissing)
                {
                    // Offer installing mod disabled
                    var installDisabled = await _uiService.ShowConfirmationAsync(
                        "Install Disabled?",
                        $"{modTitle} will not be loadable without these dependencies.\n\n" +
                        "Install the mod disabled instead?",
                        null);

                    if (!installDisabled)
                    {
                        result.Proceed = false;
                        SetStatus($"Cancelled installation of {modTitle} due to missing dependencies.", LogLevel.Warning);
                        return result;
                    }

                    result.InstallEnabled = false;
                }
                else
                {
                    result.MissingDependenciesToInstall.AddRange(missingDeps);
                }
            }

            // 2) Installed but disabled deps
            if (installedDisabledDeps.Count > 0 && result.InstallEnabled)
            {
                var msg =
                    $"The following mandatory dependencies for {modTitle} are installed but disabled:\n\n" +
                    string.Join("\n", installedDisabledDeps.Select(m => m.Title)) +
                    "\n\nEnable them now?";

                var enableDeps = await _uiService.ShowConfirmationAsync(
                    "Enable Dependencies",
                    msg,
                    null);

                if (enableDeps)
                {
                    result.ModsToEnable.AddRange(installedDisabledDeps);
                }
                else
                {
                    // Offer to install main mod disabled instead
                    var installDisabled = await _uiService.ShowConfirmationAsync(
                        "Install Disabled?",
                        $"{modTitle} will be installed disabled and its dependencies will remain disabled.\n\nContinue?",
                        null);

                    if (!installDisabled)
                    {
                        result.Proceed = false;
                        SetStatus($"Cancelled installation of {modTitle} due to disabled dependencies.", LogLevel.Warning);
                        return result;
                    }

                    result.InstallEnabled = false;
                }
            }

            // 3) Incompatible enabled mods
            if (incompatibleLoaded.Count > 0 && result.InstallEnabled)
            {
                var msg =
                    $"The following mods are incompatible with {modTitle} and are currently enabled:\n\n" +
                    string.Join("\n", incompatibleLoaded.Select(m => m.Title)) +
                    "\n\nDo you want to disable them and install {modTitle} enabled?\n\n" +
                    "Choosing No will install the mod disabled instead.";

                var disableIncompatibles = await _uiService.ShowConfirmationAsync(
                    "Incompatible Mods",
                    msg,
                    null);

                if (disableIncompatibles)
                {
                    result.ModsToDisable.AddRange(incompatibleLoaded);
                }
                else
                {
                    result.InstallEnabled = false;
                }
            }

            return result;
        }
    }
}