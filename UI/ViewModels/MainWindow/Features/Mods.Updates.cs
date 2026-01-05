using FactorioModManager.Models;
using FactorioModManager.Services;
using FactorioModManager.ViewModels.MainWindow.UpdateHandlers;
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
        // Progress properties for Download progress
        private bool _isUpdatingAll;

        private bool _isDownloadProgressVisible;
        private int _downloadProgressTotal;
        private int _downloadProgressCompleted;

        // Throttle interval for batching progress UI updates
        private static readonly TimeSpan _updateProgressUiThrottle = TimeSpan.FromMilliseconds(250);

        // Debounced targeted refresh state for affected mods to reduce UI churn
        private readonly Lock _refreshAffectedLock = new();

        private readonly HashSet<string> _refreshAffectedNames = new(StringComparer.OrdinalIgnoreCase);

        // Updates host instance
        private IUpdateHost? _updatesHost;

        // Apply any batched progress to the UI. Intended for use by UpdatesHost when its timer fires.
        internal void ApplyBatchedProgressToUi()
        {
            _uiService.Post(() =>
            {
                // Set the UI-bound property once (triggers UpdateProgressText which will start animation)
                DownloadProgressCompleted = _downloadProgressCompleted;

                // Speed text is owned by the DownloadProgress helper; nothing to reapply here

                // Compute a sensible percent target (prefer download totals when present)
                double targetPercent = 0.0;
                if (DownloadProgressTotal > 0)
                    targetPercent = (double)DownloadProgressCompleted / Math.Max(1, DownloadProgressTotal) * 100.0;

                try
                {
                    var helper = GetOrCreateDownloadProgressHelper();
                    helper.SetTargetPercent(targetPercent);
                    helper.StartAnimation();
                }
                catch (Exception ex)
                {
                    _logService.LogDebug($"Failed to start progress helper animation: {ex.Message}");
                }
            });
        }

        public bool IsUpdatingAll
        {
            get => _isUpdatingAll;
            set => this.RaiseAndSetIfChanged(ref _isUpdatingAll, value);
        }

        public bool IsDownloadProgressVisible
        {
            get => _isDownloadProgressVisible;
            set => this.RaiseAndSetIfChanged(ref _isDownloadProgressVisible, value);
        }

        public int DownloadProgressTotal
        {
            get => _downloadProgressTotal;
            set
            {
                this.RaiseAndSetIfChanged(ref _downloadProgressTotal, value);
                UpdateProgressText();
            }
        }

        public int DownloadProgressCompleted
        {
            get => _downloadProgressCompleted;
            set
            {
                this.RaiseAndSetIfChanged(ref _downloadProgressCompleted, value);
                UpdateProgressText();
            }
        }

        private void UpdateProgressText()
        {
            var text = DownloadProgressTotal > 0 ? $"{DownloadProgressCompleted}/{DownloadProgressTotal}" : string.Empty;
            try { DownloadProgress.UpdateProgressText(text); } catch (Exception ex) { _logService.LogDebug($"UpdateProgressText failed: {ex.Message}"); }
        }

        // Helper to begin a single-download progress UI (used by single-mod install/update flows)
        internal async Task BeginSingleDownloadProgressAsync()
        {
            try
            {
                await _uiService.InvokeAsync(() =>
                {
                    DownloadProgressTotal = 1;
                    DownloadProgressCompleted = 0;
                    IsDownloadProgressVisible = true;
                });
            }
            catch (Exception ex)
            {
                _logService.LogDebug($"BeginSingleDownloadProgressAsync failed: {ex.Message}");
            }
        }

        // Helper to end a single-download progress UI. If 'minimal' is true, perform a quick teardown used by lightweight installs.
        internal async Task EndSingleDownloadProgressAsync(bool minimal = false)
        {
            try
            {
                // Timer and scheduling are owned by the host now; nothing to flush/dispose here.

                if (!minimal)
                {
                    try { GetOrCreateDownloadProgressHelper().StopAndSetPercent(100.0); } catch (Exception ex) { _logService.LogDebug($"StopAndSetPercent final failed: {ex.Message}"); }
                    try { await Task.Delay(400); } catch { }
                    try { DownloadProgress.UpdateSpeedText(null); } catch { }
                    try { DownloadProgress.UpdateProgressText(string.Empty); } catch { }
                }

                await _uiService.InvokeAsync(() =>
                {
                    if (minimal)
                    {
                        DownloadProgressCompleted = 1;
                        IsDownloadProgressVisible = false;
                        DownloadProgressTotal = 0;
                        DownloadProgressCompleted = 0;
                        try { DownloadProgress.UpdateSpeedText(null); } catch { }
                        try { DownloadProgress.UpdateProgressPercent(0.0); } catch { }
                    }
                    else
                    {
                        IsDownloadProgressVisible = false;
                        Interlocked.Exchange(ref _downloadProgressCompleted, 0);
                        DownloadProgressCompleted = 0;
                        DownloadProgressTotal = 0;
                        try { DownloadProgress.UpdateProgressPercent(0.0); } catch { }
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.LogDebug($"EndSingleDownloadProgressAsync failed: {ex.Message}");
            }
        }

        // Wrapper to update a single mod using the new core flow
        private async Task UpdateSingleAsync(ModViewModel? mod)
        {
            if (mod == null) return;
            var host = EnsureUpdatesHost();
            await host.UpdateSingleAsync(mod);
        }

        /// <summary>
        /// Update all mods that currently have pending updates. Processes mods with limited concurrency
        /// and will attempt to auto-install missing dependencies if user confirms.
        /// </summary>
        private async Task UpdateAllAsync()
        {
            // Begin aggregating candidate dependency names for a single targeted recompute at the end
            StartCandidateAggregation();

            var host = EnsureUpdatesHost();
            await host.UpdateAllAsync();

            // Host performs refreshes; finalize VM aggregation state
            EndCandidateAggregationAndRecompute();

            // If there are no updates remaining, clear the Pending Updates filter so the list doesn't remain empty
            try
            {
                await _uiService.InvokeAsync(() =>
                {
                    if (!HasUpdates && ShowOnlyPendingUpdates)
                    {
                        ShowOnlyPendingUpdates = false;
                        SetStatus("All updates applied. Pending updates filter cleared.");
                    }
                });
            }
            catch (Exception ex) { _logService.LogDebug($"Clearing pending updates filter failed: {ex.Message}"); }
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
                        this.RaisePropertyChanged(nameof(EnabledCountText));
                        // ensure filtered view reflects newly-found updates
                        if (ShowOnlyPendingUpdates)
                            ApplyModFilter();
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
                                    this.RaisePropertyChanged(nameof(EnabledCountText));
                                    SetStatus($"Update available for {SelectedMod.Title}: {SelectedMod.Version} → {latestVersion}");
                                });

                                // Ensure pending-updates filter reflects this new item immediately
                                if (ShowOnlyPendingUpdates)
                                    ApplyModFilter();
                            }
                            else
                            {
                                _metadataService.UpdateLatestVersion(SelectedMod.Name, latestVersion, hasUpdate: false);
                                await _uiService.InvokeAsync(() =>
                                {
                                    SelectedMod.HasUpdate = false;
                                    SelectedMod.LatestVersion = null;
                                    this.RaisePropertyChanged(nameof(EnabledCountText));
                                    SetStatus($"{SelectedMod.Title} is up to date (version {SelectedMod.Version})");
                                });

                                if (ShowOnlyPendingUpdates)
                                    ApplyModFilter();
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

        // Force immediate refresh and wait for completion (no debounce)
        private async Task ForceRefreshAffectedModsAsync(IEnumerable<string> modNames)
        {
            var names = modNames?.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
            if (names.Count == 0) return;

            // Cancel any pending debounced refresh for these names by removing them from pending set
            lock (_refreshAffectedLock)
            {
                foreach (var n in names) _refreshAffectedNames.Remove(n);
            }

            await DoRefreshAffectedModsAsync(names);
        }

        private async Task DoRefreshAffectedModsAsync(IEnumerable<string> names)
        {
            var list = names?.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
            if (list.Count == 0) return;

            // Refresh version caches on background thread
            await Task.Run(() =>
            {
                foreach (var name in list)
                {
                    try { _modVersionManager.RefreshVersionCache(name); } catch (Exception ex) { _logService.LogDebug($"RefreshVersionCache failed for {name}: {ex.Message}"); }
                }
            });

            // Update UI-bound VM properties for each affected mod (batch on UI thread)
            await _uiService.InvokeAsync(() =>
            {
                foreach (var name in list)
                {
                    try
                    {
                        var vm = _allMods.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                        if (vm == null) continue;

                        try { LoadModVersions(vm); } catch (Exception ex) { _logService.LogDebug($"LoadModVersions failed for {name}: {ex.Message}"); }

                        try
                        {
                            // Apply metadata directly into VM
                            vm.HasUpdate = _metadataService.GetHasUpdate(name);
                            vm.LatestVersion = _metadataService.GetLatestVersion(name);
                        }
                        catch (Exception ex) { _logService.LogDebug($"Applying metadata failed for {name}: {ex.Message}"); }

                        // Rely on property setters to raise most notifications; raise dependent computed properties explicitly
                        vm.RaisePropertyChanged(nameof(vm.HasMultipleVersions));
                        vm.RaisePropertyChanged(nameof(vm.UpdateText));
                    }
                    catch (Exception ex) { _logService.LogDebug($"Error updating VM for {name}: {ex.Message}"); }
                }

                // Batch notifications for counts/filters once
                this.RaisePropertyChanged(nameof(EnabledCountText));
                this.RaisePropertyChanged(nameof(UpdatesAvailableCount));
                this.RaisePropertyChanged(nameof(HasUpdates));
                this.RaisePropertyChanged(nameof(UpdatesCountText));
                if (ShowOnlyPendingUpdates)
                    ApplyModFilter();
            });
        }

        private Task<bool> ConfirmDependencyInstallAsync(
            bool suppress,
            string title,
            string message,
            string confirmText,
            string cancelText)
        {
            if (suppress)
                return Task.FromResult(true);

            return _uiService.ShowConfirmationAsync(title, message, null, confirmText, cancelText);
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
                        // Use LatestVersion when building expected filename for the update zip
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
                            this.RaisePropertyChanged(nameof(EnabledCountText));
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

        // Ensure a single UpdatesHost is created with injected services and delegates
        private IUpdateHost EnsureUpdatesHost()
        {
            if (_updatesHost != null) return _updatesHost;

            _updatesHost = new UpdatesHost(
                allMods: _allMods,
                dependencyFlow: _dependencyFlow,
                modService: _modService,
                logService: _logService,
                uiService: _uiService,
                downloadService: _downloadService,
                apiService: _apiService,
                settingsService: _settingsService,
                metadataService: _metadataService,
                forceRefreshAffectedModsAsync: ForceRefreshAffectedModsAsync,
                beginSingleDownloadProgressAsync: BeginSingleDownloadProgressAsync,
                endSingleDownloadProgressAsync: EndSingleDownloadProgressAsync,
                incrementBatchCompleted: () => Interlocked.Increment(ref _downloadProgressCompleted),
                setStatusAsync: (msg, lvl) => _uiService.InvokeAsync(() => SetStatus(msg, lvl)),
                confirmDependencyInstallAsync: ConfirmDependencyInstallAsync,
                // new progress delegates
                setDownloadProgressTotal: total => _uiService.Post(() => { DownloadProgressTotal = total; }),
                setDownloadProgressCompleted: completed => _uiService.Post(() => { Interlocked.Exchange(ref _downloadProgressCompleted, completed); DownloadProgressCompleted = completed; }),
                // increment delegate should ONLY increment; host will schedule UI update
                incrementDownloadProgressCompleted: () => Interlocked.Increment(ref _downloadProgressCompleted),
                setDownloadProgressVisible: visible => _uiService.Post(() => IsDownloadProgressVisible = visible),
                // pass VM flush delegate for host timer to call
                onApplyBatchedProgress: ApplyBatchedProgressToUi
             );

            return _updatesHost!;
        }
    }
}