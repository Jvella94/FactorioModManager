using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using FactorioModManager.Models;
using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Mods;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Settings;

namespace FactorioModManager.ViewModels.MainWindow.UpdateHandlers
{
    public sealed class UpdatesHost(
        IEnumerable<ModViewModel> allMods,
        IDependencyFlow dependencyFlow,
        IModService modService,
        ILogService logService,
        IUIService uiService,
        IDownloadService downloadService,
        IFactorioApiService apiService,
        ISettingsService settingsService,
        IModMetadataService metadataService,
        Func<IEnumerable<string>, Task> forceRefreshAffectedModsAsync,
        Func<Task> beginSingleDownloadProgressAsync,
        Func<bool, Task> endSingleDownloadProgressAsync,
        Action incrementBatchCompleted,
        Func<string, LogLevel, Task> setStatusAsync,
        Func<bool, string, string, string, string, Task<bool>> confirmDependencyInstallAsync,
        Action<int> setDownloadProgressTotal,
        Action<int> setDownloadProgressCompleted,
        Action incrementDownloadProgressCompleted,
        Action<bool> setDownloadProgressVisible,
        Action onApplyBatchedProgress) : IUpdateHost
    {
        private readonly IEnumerable<ModViewModel> _allMods = allMods ?? throw new ArgumentNullException(nameof(allMods));
        private readonly IDependencyFlow _dependencyFlow = dependencyFlow ?? throw new ArgumentNullException(nameof(dependencyFlow));
        private readonly IModService _modService = modService ?? throw new ArgumentNullException(nameof(modService));
        private readonly ILogService _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        private readonly IUIService _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
        private readonly IDownloadService _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
        private readonly IFactorioApiService _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        private readonly ISettingsService _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        private readonly IModMetadataService _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));

        private readonly Func<IEnumerable<string>, Task> _forceRefreshAffectedModsAsync = forceRefreshAffectedModsAsync ?? throw new ArgumentNullException(nameof(forceRefreshAffectedModsAsync));
        private readonly Func<Task> _beginSingleDownloadProgress = beginSingleDownloadProgressAsync ?? throw new ArgumentNullException(nameof(beginSingleDownloadProgressAsync));
        private readonly Func<bool, Task> _endSingleDownloadProgress = endSingleDownloadProgressAsync ?? throw new ArgumentNullException(nameof(endSingleDownloadProgressAsync));
        private readonly Action _incrementBatchCompleted = incrementBatchCompleted ?? throw new ArgumentNullException(nameof(incrementBatchCompleted));
        private readonly Func<string, LogLevel, Task> _setStatusAsync = setStatusAsync ?? throw new ArgumentNullException(nameof(setStatusAsync));
        private readonly Func<bool, string, string, string, string, Task<bool>> _confirmDependencyInstall = confirmDependencyInstallAsync ?? throw new ArgumentNullException(nameof(confirmDependencyInstallAsync));
        private readonly Action<int> _setDownloadProgressTotal = setDownloadProgressTotal ?? throw new ArgumentNullException(nameof(setDownloadProgressTotal));
        private readonly Action<int> _setDownloadProgressCompleted = setDownloadProgressCompleted ?? throw new ArgumentNullException(nameof(setDownloadProgressCompleted));
        private readonly Action _incrementDownloadProgressCompleted = incrementDownloadProgressCompleted ?? throw new ArgumentNullException(nameof(incrementDownloadProgressCompleted));
        private readonly Action<bool> _setDownloadProgressVisible = setDownloadProgressVisible ?? throw new ArgumentNullException(nameof(setDownloadProgressVisible));
        private readonly Action _applyBatchedProgressAction = onApplyBatchedProgress ?? throw new ArgumentNullException(nameof(onApplyBatchedProgress));

        // guard to avoid double-starting progress when main install delegate calls back into host.InstallModAsync
        private volatile bool _runInstallFlowActive = false;

        private readonly TimeSpan _updateProgressUiThrottle = TimeSpan.FromMilliseconds(250);
        private System.Timers.Timer? _progressTimer;
        private readonly Lock _progressTimerLock = new();
        private bool _progressPending = false;

        // Host-owned scheduling: start/stop timer and call VM flush delegate when elapsed
        private void HostScheduleBatchedProgressUiUpdate()
        {
            lock (_progressTimerLock)
            {
                _progressPending = true;
                if (_progressTimer == null)
                {
                    _progressTimer = new System.Timers.Timer(_updateProgressUiThrottle.TotalMilliseconds) { AutoReset = false };
                    _progressTimer.Elapsed += (s, e) =>
                    {
                        lock (_progressTimerLock)
                        {
                            if (!_progressPending) return;
                            _progressPending = false;
                        }
                        try
                        {
                            _applyBatchedProgressAction();
                        }
                        catch { }
                    };
                }

                try
                {
                    _progressTimer.Stop();
                    _progressTimer.Start();
                }
                catch { }
            }
        }

        private void HostDisposeProgressTimer()
        {
            lock (_progressTimerLock)
            {
                try { _progressTimer?.Stop(); } catch { }
                try { _progressTimer?.Dispose(); } catch { }
                _progressTimer = null;
                _progressPending = false;
            }
        }

        ~UpdatesHost()
        {
            HostDisposeProgressTimer();
        }

        // IUpdateHost surface
        public IDependencyFlow DependencyFlow => _dependencyFlow;

        public IEnumerable<ModViewModel> AllMods => _allMods;
        public IModService ModService => _modService;
        public ILogService LogService => _logService;

        public Task BeginSingleDownloadProgressAsync() => _beginSingleDownloadProgress();

        public Task EndSingleDownloadProgressAsync(bool minimal = false) => _endSingleDownloadProgress(minimal);

        public void IncrementBatchCompleted() => _incrementBatchCompleted();

        // Expose host scheduling through IUpdateHost
        /// <summary>
        /// Schedule a batched UI update for aggregated download progress.
        /// The host owns the timer and will invoke the VM-provided action when the throttle elapses.
        /// </summary>
        public void ScheduleBatchedProgressUiUpdate() => HostScheduleBatchedProgressUiUpdate();

        /// <summary>
        /// Set status text on the VM via the provided delegate.
        /// </summary>
        public Task SetStatusAsync(string message, LogLevel level = LogLevel.Info) => _setStatusAsync(message, level);

        /// <summary>
        /// Ask the VM to confirm dependency installation (may be suppressed by caller).
        /// </summary>
        public Task<bool> ConfirmDependencyInstallAsync(bool suppress, string title, string message, string confirmText, string cancelText)
            => _confirmDependencyInstall(suppress, title, message, confirmText, cancelText);

        /// <summary>
        /// Install a mod using host-internal logic.
        /// </summary>
        public Task<Result> InstallModAsync(string modName) => InstallModInternal(modName);

        /// <summary>
        /// Force immediate refresh of affected mods in the VM.
        /// </summary>
        public Task ForceRefreshAffectedModsAsync(IEnumerable<string> names) => _forceRefreshAffectedModsAsync(names);

        public void ToggleMod(string modName, bool enabled) => _modService.ToggleMod(modName, enabled);

        // Core updater used by both single and batch flows
        public async Task UpdateModsCoreAsync(List<ModViewModel> modsToUpdate, IDependencyHandler dependencyHandler, IProgressReporter progressReporter, int concurrency)
        {
            var plannedUpdates = modsToUpdate
                .Where(m => !string.IsNullOrEmpty(m.LatestVersion))
                .ToDictionary(m => m.Name, m => m.LatestVersion!, StringComparer.OrdinalIgnoreCase);

            // Prepare dependencies (batch handler may install aggregated deps and set progress total)
            var prepared = await dependencyHandler.PrepareAsync(modsToUpdate, plannedUpdates, progressReporter);
            if (!prepared)
            {
                return;
            }

            // Ensure we have reasonable concurrency
            if (concurrency <= 0) concurrency = 1;
            var semaphore = new SemaphoreSlim(concurrency);
            var tasks = new List<Task>();
            var results = new ConcurrentBag<(string Mod, bool Success, String Message)>();

            try
            {
                foreach (var mod in modsToUpdate)
                {
                    var localMod = mod;
                    tasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            await _uiService.InvokeAsync(() => SetStatusInternal($"Updating {localMod.Title}..."));

                            try
                            {
                                var beforeHasUpdate = localMod.HasUpdate;
                                // delegate to host download/update flow
                                await DownloadUpdateInternal(localMod, suppressDependencyPrompts: dependencyHandler.SuppressDependencyPrompts);
                                var updated = beforeHasUpdate && !localMod.HasUpdate;
                                results.Add((localMod.Title, updated, updated ? "Updated" : "Skipped - update not applied"));
                            }
                            catch (Exception ex)
                            {
                                _logService.LogError($"Error updating {localMod.Name} in parallel task: {ex.Message}", ex);
                                await _uiService.InvokeAsync(() => SetStatusInternal($"Error updating {localMod.Title}: {ex.Message}", LogLevel.Error));
                                results.Add((localMod.Title, false, $"Error: {ex.Message}"));
                            }
                        }
                        finally
                        {
                            // reflect progress
                            try { progressReporter.Increment(); } catch { }
                            semaphore.Release();
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                // Build and show summary
                await _uiService.InvokeAsync(() =>
                {
                    SetStatusInternal("Update run complete. Preparing summary...");
                    // Host cannot raise VM property changed; consumer should update UI as needed after refresh
                });

                var resultList = results.ToList();
                var successCount = resultList.Count(r => r.Success);
                var failedCount = resultList.Count - successCount;

                var summary = $"Update finished. {successCount} succeeded, {failedCount} failed or skipped." + Environment.NewLine + Environment.NewLine;
                foreach (var (Mod, Success, Message) in resultList.OrderByDescending(r => r.Success))
                {
                    summary += $"- {Mod}: {(Success ? "Success" : Message)}" + Environment.NewLine;
                }

                await _uiService.ShowMessageAsync("Update Summary", summary);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                try { await _uiService.InvokeAsync(() => SetStatusInternal("")); } catch { }
            }
        }

        public async Task UpdateAllAsync()
        {
            var modsToUpdate = _allMods.Where(m => m.HasUpdate && !string.IsNullOrEmpty(m.LatestVersion)).ToList();
            if (modsToUpdate.Count == 0)
            {
                await _uiService.InvokeAsync(() => SetStatusInternal("No pending updates to apply."));
                return;
            }

            var confirmAll = await _uiService.ShowConfirmationAsync(
                "Update All Mods",
                $"This will download and install updates for {modsToUpdate.Count} mod(s). Continue?",
                null,
                "Yes",
                "No");

            if (!confirmAll)
            {
                _logService.LogDebug("UpdateAll cancelled by user");
                await _uiService.InvokeAsync(() => SetStatusInternal("Update cancelled."));
                return;
            }

            var dependencyHandler = new BatchDependencyHandler(this);
            var progressReporter = new BatchProgressReporter(this);

            var concurrency = _settingsService.GetUpdateConcurrency();
            if (concurrency <= 0) concurrency = 3;

            try
            {
                await _uiService.InvokeAsync(() => SetStatusInternal($"Starting update of {modsToUpdate.Count} mod(s)..."));

                await UpdateModsCoreAsync(modsToUpdate, dependencyHandler, progressReporter, concurrency);

                var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in modsToUpdate) affected.Add(m.Name);

                try { await _forceRefreshAffectedModsAsync(affected); } catch (Exception ex) { _logService.LogDebug($"ForceRefreshAffectedModsAsync failed: {ex.Message}"); }

                try
                {
                    await _uiService.InvokeAsync(() => SetStatusInternal("All updates applied."));
                }
                catch (Exception ex) { _logService.LogDebug($"Clearing pending updates filter failed: {ex.Message}"); }
            }
            catch (Exception ex)
            {
                _logService.LogError($"UpdateAll failed: {ex.Message}", ex);
                await _uiService.InvokeAsync(() => SetStatusInternal($"Update All failed: {ex.Message}", LogLevel.Error));
            }
        }

        public async Task UpdateSingleAsync(ModViewModel? mod)
        {
            if (mod == null) return;
            var modsToUpdate = new List<ModViewModel> { mod };
            var dependencyHandler = new SingleDependencyHandler(this);
            var progressReporter = new SingleProgressReporter(this);
            await UpdateModsCoreAsync(modsToUpdate, dependencyHandler, progressReporter, concurrency: 1);
        }

        // Internal helper: set status via UI service
        private void SetStatusInternal(string message, LogLevel level = LogLevel.Info)
        {
            try
            {
                _logService.Log(message, level);
            }
            catch { }

            // Also post status to UI via provided delegate so host messages appear in VM status
            try
            {
                _ = _setStatusAsync(message, level);
            }
            catch { }
        }

        // Internal implementation of downloading/updating a single mod (moved into host)
        private async Task DownloadUpdateInternal(ModViewModel? mod, bool suppressDependencyPrompts = false)
        {
            if (mod == null || !mod.HasUpdate || string.IsNullOrEmpty(mod.LatestVersion))
                return;

            bool singleProgress = false;

            var installedDepsDuringUpdate = new List<string>();
            var toggledModsDuringUpdate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var refreshedNamesDuringResolution = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var planned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { mod.Name, mod.LatestVersion! } };

                var updateResolution = await _dependencyFlow.ResolveForUpdateAsync(mod.Name, mod.LatestVersion!, _allMods, planned);
                if (!updateResolution.Proceed)
                {
                    _logService.LogDebug($"Update cancelled by user during dependency resolution for {mod.Name}");
                    await _uiService.InvokeAsync(() => SetStatusInternal("Update cancelled."));
                    return;
                }

                // Apply enable/disable decisions
                if (mod.IsEnabled)
                {
                    foreach (var toEnable in updateResolution.ModsToEnable)
                    {
                        var vm = _allMods.FirstOrDefault(m => m.Name == toEnable.Name);
                        if (vm != null && !vm.IsEnabled)
                        {
                            vm.IsEnabled = true;
                            _modService.ToggleMod(vm.Name, true);
                            toggledModsDuringUpdate.Add(vm.Name);
                        }
                    }
                }

                foreach (var toDisable in updateResolution.ModsToDisable)
                {
                    var vm = _allMods.FirstOrDefault(m => m.Name == toDisable.Name);
                    if (vm != null && vm.IsEnabled)
                    {
                        vm.IsEnabled = false;
                        _modService.ToggleMod(vm.Name, false);
                        toggledModsDuringUpdate.Add(vm.Name);
                    }
                }

                if (suppressDependencyPrompts && updateResolution.MissingDependenciesToInstall.Count > 0)
                {
                    _logService.LogDebug($"Skipping dependency install prompt for {mod.Name} because prompts are suppressed and dependencies remain.");
                    await _uiService.InvokeAsync(() => SetStatusInternal($"Skipping update for {mod.Title}: dependencies not installed", LogLevel.Warning));
                    return;
                }

                if (!suppressDependencyPrompts && updateResolution.MissingDependenciesToInstall.Count > 0)
                {
                    var (previewResolution, previewMessage) = await _dependencyFlow.BuildUpdatePreviewAsync(mod.Name, mod.LatestVersion!, _allMods, planned);

                    var confirmDeps = await _uiService.ShowConfirmationAsync(
                        "Install Missing Dependencies",
                        previewMessage,
                        null,
                        "Install",
                        "Skip");

                    if (!confirmDeps)
                    {
                        _logService.LogDebug($"User declined to install missing dependencies for update of {mod.Name}");
                        await _uiService.InvokeAsync(() => SetStatusInternal("Update cancelled: missing dependencies not installed", LogLevel.Warning));
                        return;
                    }

                    foreach (var toEnable in previewResolution.ModsToEnable)
                    {
                        var vm = _allMods.FirstOrDefault(m => m.Name == toEnable.Name);
                        if (vm != null && !vm.IsEnabled)
                        {
                            vm.IsEnabled = true;
                            _modService.ToggleMod(vm.Name, true);
                            toggledModsDuringUpdate.Add(vm.Name);
                        }
                    }

                    foreach (var toDisable in previewResolution.ModsToDisable)
                    {
                        var vm = _allMods.FirstOrDefault(m => m.Name == toDisable.Name);
                        if (vm != null && vm.IsEnabled)
                        {
                            vm.IsEnabled = false;
                            _modService.ToggleMod(vm.Name, false);
                            toggledModsDuringUpdate.Add(vm.Name);
                        }
                    }

                    // Install aggregated dependencies sequentially
                    foreach (var depName in previewResolution.MissingDependenciesToInstall.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        if (_allMods.Any(m => m.Name.Equals(depName, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        var installResult = await InstallModInternal(depName);
                        if (!installResult.Success)
                        {
                            _logService.LogWarning($"Failed to install dependency {depName}: {installResult.Error}");
                            await _uiService.InvokeAsync(() => SetStatusInternal($"Failed to install dependency {depName}: {installResult.Error}", LogLevel.Warning));
                            return;
                        }

                        installedDepsDuringUpdate.Add(depName);

                        await Task.Delay(200);
                    }

                    await Task.Delay(500);

                    var affectedDeps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var d in installedDepsDuringUpdate) affectedDeps.Add(d);
                    foreach (var e in previewResolution.ModsToEnable) affectedDeps.Add(e.Name);
                    foreach (var d in previewResolution.ModsToDisable) affectedDeps.Add(d.Name);
                    affectedDeps.Add(mod.Name);

                    try
                    {
                        await _forceRefreshAffectedModsAsync(affectedDeps);
                        foreach (var n in affectedDeps) refreshedNamesDuringResolution.Add(n);
                    }
                    catch (Exception ex)
                    {
                        _logService.LogDebug($"Ignored error refreshing affected mods during resolution: {ex.Message}");
                    }
                }

                // Proceed to download
                _logService.Log($"Starting update for {mod.Title} from {mod.Version} to {mod.LatestVersion}");

                await _uiService.InvokeAsync(() =>
                {
                    mod.IsDownloading = true;
                    mod.HasDownloadProgress = false;
                    mod.DownloadStatusText = $"Preparing download for {mod.Title}...";
                    SetStatusInternal($"Downloading update for {mod.Title}...");
                });

                // Begin single-download progress UI if available
                try
                {
                    await BeginSingleDownloadProgressAsync();
                    singleProgress = true;
                }
                catch { }

                var modDetails = await _apiService.GetModDetailsAsync(mod.Name);
                if (modDetails?.Releases == null)
                {
                    _logService.Log($"Failed to fetch release details for {mod.Name}", LogLevel.Error);
                    _uiService.Post(() =>
                    {
                        mod.IsDownloading = false;
                        SetStatusInternal($"Failed to fetch update details for {mod.Title}", LogLevel.Error);
                    });
                    return;
                }

                var latestRelease = modDetails.Releases.OrderByDescending(r => r.ReleasedAt).FirstOrDefault();
                if (latestRelease == null || string.IsNullOrEmpty(latestRelease.DownloadUrl))
                {
                    _logService.Log($"No download URL found for {mod.Name}", LogLevel.Error);
                    _uiService.Post(() =>
                    {
                        mod.IsDownloading = false;
                        SetStatusInternal($"No download URL available for {mod.Title}", LogLevel.Error);
                    });
                    return;
                }

                var result = await DownloadModFromPortalInternal(mod.Name, mod.Title, latestRelease.Version, latestRelease.DownloadUrl, mod);
                if (!result.Success || !result.Value)
                    return;

                var modsDirectory = FolderPathHelper.GetModsDirectory();
                var newFilePath = System.IO.Path.Combine(modsDirectory, $"{mod.Name}_{latestRelease.Version}.zip");
                _downloadService.DeleteOldVersions(mod.Name, newFilePath);

                _logService.Log($"Successfully updated {mod.Title} to version {latestRelease.Version}");
                _metadataService.UpdateLatestVersion(mod.Name, latestRelease.Version, hasUpdate: false);

                await _uiService.InvokeAsync(() =>
                {
                    mod.HasUpdate = false;
                    mod.LatestVersion = null;
                    mod.IsDownloading = false;
                    mod.DownloadStatusText = "Update complete!";
                    SetStatusInternal($"Update complete for {mod.Title}. Refreshing...");
                });

                await Task.Delay(500);

                try
                {
                    var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mod.Name };
                    foreach (var d in installedDepsDuringUpdate) affected.Add(d);
                    foreach (var t in toggledModsDuringUpdate) affected.Add(t);
                    affected.ExceptWith(refreshedNamesDuringResolution);

                    if (affected.Count > 0)
                    {
                        try { await _forceRefreshAffectedModsAsync(affected); } catch (Exception ex) { _logService.LogDebug($"Ignored error refreshing affected mods after install: {ex.Message}"); }
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogDebug($"Error preparing targeted refresh set: {ex.Message}");
                }

                await _uiService.InvokeAsync(() =>
                {
                    var updatedMod = _allMods.FirstOrDefault(m => m.Name == mod.Name);
                    if (updatedMod != null)
                    {
                        updatedMod.SelectedVersion = updatedMod.Version;
                        // caller VM will set SelectedMod on refresh callback
                        SetStatusInternal($"Successfully updated {updatedMod.Title} to {updatedMod.Version}");
                        _logService.Log($"Reselected updated mod: {updatedMod.Title}");
                    }
                    else
                    {
                        SetStatusInternal($"Update complete but could not find mod {mod.Name}", LogLevel.Warning);
                        _logService.Log($"Warning: Could not find mod {mod.Name} after refresh", LogLevel.Warning);
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.LogError($"Update error details: {ex.Message}", ex);
                await _uiService.InvokeAsync(() =>
                {
                    if (mod != null)
                    {
                        mod.IsDownloading = false;
                        mod.DownloadStatusText = $"Error: {ex.Message}";
                    }
                    SetStatusInternal($"Error updating {mod?.Title}: {ex.Message}", LogLevel.Error);
                });
            }
            finally
            {
                if (singleProgress)
                {
                    await EndSingleDownloadProgressInternal(false);
                }
            }
        }

        // Internal download helper that uses injected download service
        private async Task<Result<bool>> DownloadModFromPortalInternal(
            string modName,
            string modTitle,
            string version,
            string downloadUrl,
            ModViewModel? modForProgress = null)
        {
            try
            {
                IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null;

                if (modForProgress != null)
                {
                    var globalReporter = _downloadService is IDownloadProgress dp ? dp.CreateGlobalDownloadProgressReporter() : null;
                    if (globalReporter != null)
                    {
                        progress = new Progress<(long bytesDownloaded, long? totalBytes)>(p =>
                        {
                            _uiService.Post(() =>
                            {
                                modForProgress.HasDownloadProgress = p.totalBytes.HasValue && p.totalBytes.Value > 0;
                                if (p.totalBytes.HasValue && p.totalBytes.Value > 0)
                                {
                                    var progressPercent = (double)p.bytesDownloaded / p.totalBytes.Value * 100;
                                    modForProgress.DownloadProgress = progressPercent;
                                    if (p.totalBytes.HasValue && p.totalBytes.Value > 0)
                                        modForProgress.DownloadStatusText = $"Downloading... {progressPercent:F0}%";
                                }
                                else
                                {
                                    var mbDownloaded = p.bytesDownloaded / 1024.0 / 1024.0;
                                    modForProgress.DownloadStatusText = $"Downloading... {mbDownloaded:F2} MB";
                                }
                            });

                            try { globalReporter.Report(p); } catch { }
                        });
                    }
                }
                else
                {
                    // no per-mod UI; use global reporter if available
                    if (_downloadService is IDownloadProgress dp2)
                        progress = dp2.CreateGlobalDownloadProgressReporter();
                }

                var result = await _downloadService.DownloadModAsync(modName, modTitle, version, downloadUrl, progress);

                if (!result.Success)
                {
                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatusInternal($"Download failed for {modTitle}: {result.Error}", LogLevel.Error);
                        modForProgress?.DownloadStatusText = $"Failed: {result.Error}";
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                HandleErrorInternal(ex, $"Error downloading {modTitle}: {ex.Message}");
                return Result<bool>.Fail(ex.Message, ErrorCode.DownloadFailed);
            }
        }

        private Task EndSingleDownloadProgressInternal(bool minimal = false)
        {
            return _endSingleDownloadProgress(minimal);
        }

        // Internal install logic (from VM.InstallMod)
        private async Task<Result> InstallModInternal(string modName)
        {
            var singleProgress = false;

            try
            {
                var modDetails = await _apiService.GetModDetailsAsync(modName);
                if (modDetails?.Releases == null || modDetails.Releases.Count == 0)
                {
                    _logService.LogWarning($"Failed to fetch release details for {modName}");
                    await _uiService.InvokeAsync(() => SetStatusInternal($"Failed to fetch mod details for {modName}", LogLevel.Error));
                    return Result.Fail("No release information found", ErrorCode.ApiRequestFailed);
                }

                var latestRelease = modDetails.Releases.OrderByDescending(r => r.ReleasedAt).FirstOrDefault();
                if (latestRelease == null || string.IsNullOrEmpty(latestRelease.DownloadUrl))
                {
                    _logService.LogWarning($"No download URL found for {modName}");
                    await _uiService.InvokeAsync(() => SetStatusInternal($"No download URL available for {modName}", LogLevel.Error));
                    return Result.Fail("No download URL", ErrorCode.ApiRequestFailed);
                }

                await _uiService.InvokeAsync(() => SetStatusInternal($"Downloading {modName}..."));

                // Begin single-download progress UI if available
                try
                {
                    // If we're running as the main install inside RunInstallWithDependenciesAsync, the host may have already
                    // started the single-download progress UI. Avoid double-starting by checking guard flag.
                    if (!_runInstallFlowActive)
                    {
                        await BeginSingleDownloadProgressAsync();
                        singleProgress = true;
                    }
                }
                catch { }

                var downloadResult = await _downloadService.DownloadModAsync(modName, modDetails.Title ?? modName, latestRelease.Version, latestRelease.DownloadUrl);
                if (!downloadResult.Success)
                    return downloadResult;

                return Result.Ok();
            }
            catch (Exception ex)
            {
                HandleErrorInternal(ex, $"Error installing mod {modName}");
                return Result.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
            finally
            {
                if (singleProgress)
                {
                    await EndSingleDownloadProgressInternal(minimal: true);
                }
            }
        }

        private void HandleErrorInternal(Exception ex, string context)
        {
            try { _logService.LogError(context, ex); } catch { }
        }

        /// <summary>
        /// Orchestrates installing a mod with dependency resolution and optional main install action.
        /// The provided installMainAsync should perform the actual installation of the main mod (download or local file install)
        /// and return a Result indicating success/failure.
        /// </summary>
        public async Task<Result> RunInstallWithDependenciesAsync(string modName, Func<Task<Result>> installMainAsync)
        {
            if (string.IsNullOrWhiteSpace(modName))
                return Result.Fail("Invalid mod name", ErrorCode.InvalidInput);

            try
            {
                var resolution = await _dependencyFlow.ResolveForInstallAsync(modName, _allMods);
                if (!resolution.Proceed)
                    return Result.Fail("Installation cancelled by user due to dependencies.", ErrorCode.OperationCancelled);

                // Show aggregated progress for dependencies + main
                var totalToInstall = resolution.MissingDependenciesToInstall.Distinct(StringComparer.OrdinalIgnoreCase).Count() + 1;
                try
                {
                    if (totalToInstall == 1)
                        await BeginSingleDownloadProgressAsync();
                }
                catch { }

                // Apply enable/disable decisions on UI thread
                try
                {
                    await _uiService.InvokeAsync(() =>
                    {
                        foreach (var toEnable in resolution.ModsToEnable)
                        {
                            var vm = _allMods.FirstOrDefault(m => m.Name == toEnable.Name);
                            if (vm != null && !vm.IsEnabled)
                            {
                                vm.IsEnabled = true;
                                _modService.ToggleMod(vm.Name, true);
                            }
                        }

                        foreach (var toDisable in resolution.ModsToDisable)
                        {
                            var vm = _allMods.FirstOrDefault(m => m.Name == toDisable.Name);
                            if (vm != null && vm.IsEnabled)
                            {
                                vm.IsEnabled = false;
                                _modService.ToggleMod(vm.Name, false);
                            }
                        }
                    });
                }
                catch { }

                var installedDeps = new List<string>();

                try
                {
                    // Install dependencies sequentially
                    foreach (var dep in resolution.MissingDependenciesToInstall.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        // Skip if already installed during resolution or present in _allMods
                        if (_allMods.Any(m => m.Name.Equals(dep, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        var depResult = await InstallModAsync(dep);
                        if (!depResult.Success)
                        {
                            try { await _setStatusAsync($"Failed to install dependency {dep}: {depResult.Error}", LogLevel.Warning); } catch { }
                            return Result.Fail(depResult.Error ?? "Dependency install failed", depResult.Code);
                        }

                        installedDeps.Add(dep);

                        // reflect progress in batch mode
                        try { IncrementDownloadProgressCompleted(); } catch { }
                        // ScheduleUpdateAllProgressUiUpdate is now handled by the host delegate
                    }

                    // Install main mod via provided delegate
                    Result mainResult;
                    try
                    {
                        _runInstallFlowActive = true;
                        mainResult = await installMainAsync();
                    }
                    finally
                    {
                        _runInstallFlowActive = false;
                    }

                    if (!mainResult.Success)
                        return Result.Fail(mainResult.Error ?? "Main install failed", mainResult.Code);

                    // reflect completion for main
                    try { IncrementDownloadProgressCompleted(); } catch { }
                    // ScheduleUpdateAllProgressUiUpdate is handled in the host delegate

                    // Refresh affected mods (installed deps + enabled/disabled toggles + main)
                    var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var d in installedDeps) affected.Add(d);
                    foreach (var e in resolution.ModsToEnable) affected.Add(e.Name);
                    foreach (var d in resolution.ModsToDisable) affected.Add(d.Name);
                    affected.Add(modName);

                    try { await _forceRefreshAffectedModsAsync(affected); } catch { }

                    // Notify success
                    try { await _setStatusAsync($"Successfully installed {modName}", LogLevel.Info); } catch { }

                    return Result.Ok();
                }
                finally
                {
                    try { await EndSingleDownloadProgressAsync(minimal: true); } catch { }
                }
            }
            catch (Exception ex)
            {
                try { await _setStatusAsync($"Error installing {modName}: {ex.Message}", LogLevel.Error); } catch { }
                return Result.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        // Implement progress control methods
        public void SetDownloadProgressTotal(int total) => _setDownloadProgressTotal(total);

        public void SetDownloadProgressCompleted(int completed) => _setDownloadProgressCompleted(completed);

        /// <summary>
        /// Increment the completed download count and schedule a batched UI update.
        /// </summary>
        public void IncrementDownloadProgressCompleted()
        {
            _incrementDownloadProgressCompleted();
            try { HostScheduleBatchedProgressUiUpdate(); } catch { }
        }

        /// <summary>
        /// Show or hide the download progress UI.
        /// </summary>
        public void SetDownloadProgressVisible(bool visible) => _setDownloadProgressVisible(visible);
    }
}