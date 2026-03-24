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
        IUpdateHostUi uiCallbacks) : IUpdateHost, IDisposable
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

        // Compact UI callback surface
        private readonly IUpdateHostUi _uiCallbacks = uiCallbacks ?? throw new ArgumentNullException(nameof(uiCallbacks));

        // Keep some private fields (do not rename existing ones if present elsewhere)
        private volatile bool _runInstallFlowActive = false;

        private volatile bool _isBatchUpdateInProgress = false;
        private volatile bool _isBatchDependencyInstallInProgress = false;

        private readonly TimeSpan _updateProgressUiThrottle = TimeSpan.FromMilliseconds(250);
        private Timer? _progressTimer;
        private readonly Lock _progressTimerLock = new();
        private bool _progressPending = false;
        private int _currentProgressTotal = 0;
        private int _currentProgressCompleted = 0;

        // Cancellation and lifecycle
        private readonly CancellationTokenSource _cts = new();

        // IUpdateHost surface
        public IDependencyFlow DependencyFlow => _dependencyFlow;

        public IEnumerable<ModViewModel> AllMods => _allMods;
        public IModService ModService => _modService;
        public ILogService LogService => _logService;

        // UI helpers mapped to callbacks
        public Task BeginSingleDownloadProgressAsync() => _uiCallbacks.BeginSingleDownloadProgressAsync();

        public Task EndSingleDownloadProgressAsync(bool minimal = false) => _uiCallbacks.EndSingleDownloadProgressAsync(minimal);

        public void IncrementBatchCompleted() => _uiCallbacks.IncrementBatchCompleted();

        public void ScheduleBatchedProgressUiUpdate() => HostScheduleBatchedProgressUiUpdate();

        public Task SetStatusAsync(string message, LogLevel level = LogLevel.Info) => _uiCallbacks.SetStatusAsync(message, level);

        public Task<bool> ConfirmDependencyInstallAsync(bool suppress, string title, string message, string confirmText, string cancelText)
            => _uiCallbacks.ConfirmDependencyInstallAsync(suppress, title, message, confirmText, cancelText);

        public Task<Result> InstallModAsync(string modName) => InstallModInternal(modName, CancellationToken.None);

        public Task ForceRefreshAffectedModsAsync(IEnumerable<string> names) => _uiCallbacks.ForceRefreshAffectedModsAsync(names);

        public void ToggleMod(string modName, bool enabled) => _modService.ToggleMod(modName, enabled);

        // New: public Update methods accept optional cancellation token
        public Task UpdateAllAsync(CancellationToken cancellationToken = default) => UpdateAllInternalAsync(cancellationToken);

        public Task UpdateSingleAsync(ModViewModel? mod, CancellationToken cancellationToken = default)
        {
            if (mod == null) return Task.CompletedTask;
            return UpdateSingleInternalAsync(mod, cancellationToken);
        }

        // Host-owned scheduling using System.Threading.Timer
        private void HostScheduleBatchedProgressUiUpdate()
        {
            lock (_progressTimerLock)
            {
                _progressPending = true;
                if (_progressTimer == null)
                {
                    _logService.LogDebug("Initializing progress update timer (threadpool)");
                    // Timer callback runs on threadpool
                    _progressTimer = new Timer(_ =>
                    {
                        try
                        {
                            lock (_progressTimerLock)
                            {
                                if (!_progressPending) return;
                                _progressPending = false;
                            }
                            _logService.LogDebug($"Applying batched progress update: {_currentProgressCompleted}/{_currentProgressTotal}");
                            try { _uiCallbacks.ApplyBatchedProgress(); } catch (Exception ex) { _logService.LogDebug($"ApplyBatchedProgress callback failed: {ex.Message}"); }
                        }
                        catch (Exception ex)
                        {
                            _logService.LogDebug($"Progress timer callback failed: {ex.Message}");
                        }
                    }, null, Timeout.Infinite, Timeout.Infinite);
                }

                try
                {
                    _progressTimer.Change((int)_updateProgressUiThrottle.TotalMilliseconds, Timeout.Infinite);
                    _logService.LogDebug($"Scheduled batched progress UI update (current: {_currentProgressCompleted}/{_currentProgressTotal})");
                }
                catch (Exception ex)
                {
                    _logService.LogDebug($"Failed to schedule progress timer: {ex.Message}");
                }
            }
        }

        private void HostDisposeProgressTimer()
        {
            lock (_progressTimerLock)
            {
                try { _progressTimer?.Change(Timeout.Infinite, Timeout.Infinite); } catch (Exception ex) { _logService.LogDebug($"Stop progress timer failed: {ex.Message}"); }
                try { _progressTimer?.Dispose(); } catch (Exception ex) { _logService.LogDebug($"Dispose progress timer failed: {ex.Message}"); }
                _progressTimer = null;
                _progressPending = false;
                _logService.LogDebug("Progress timer disposed");
            }
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch (Exception ex) { _logService.LogDebug($"Cancellation failed in Dispose: {ex.Message}"); }
            try { HostDisposeProgressTimer(); } catch (Exception ex) { _logService.LogDebug($"Dispose progress timer failed in Dispose: {ex.Message}"); }
            try { _cts.Dispose(); } catch (Exception ex) { _logService.LogDebug($"Disposing CTS failed: {ex.Message}"); }
        }

        // Core updater used by both single and batch flows
        public async Task UpdateModsCoreAsync(List<ModViewModel> modsToUpdate, IDependencyHandler dependencyHandler, IProgressReporter progressReporter, int concurrency, CancellationToken cancellationToken = default)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            var token = linkedCts.Token;

            var plannedUpdates = modsToUpdate
                .Where(m => !string.IsNullOrEmpty(m.LatestVersion))
                .ToDictionary(m => m.Name, m => m.LatestVersion!, StringComparer.OrdinalIgnoreCase);

            // Prepare dependencies (batch handler may install aggregated deps and set progress total)
            var prepared = await dependencyHandler.PrepareAsync(modsToUpdate, plannedUpdates, progressReporter);
            if (!prepared) return;
            token.ThrowIfCancellationRequested();

            _isBatchUpdateInProgress = true;

            try
            {
                if (concurrency <= 0) concurrency = 1;
                var semaphore = new SemaphoreSlim(concurrency);
                var tasks = new List<Task>();
                var results = new ConcurrentBag<(string Mod, bool Success, string Message)>();

                foreach (var mod in modsToUpdate)
                {
                    var localMod = mod;
                    tasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync(token).ConfigureAwait(false);
                        try
                        {
                            token.ThrowIfCancellationRequested();
                            await _uiService.InvokeAsync(() => SetStatusInternal($"Updating {localMod.Title}..."));

                            try
                            {
                                var beforeHasUpdate = localMod.HasUpdate;
                                await DownloadUpdateInternal(localMod, suppressDependencyPrompts: dependencyHandler.SuppressDependencyPrompts, cancellationToken: token);
                                var updated = beforeHasUpdate && !localMod.HasUpdate;
                                results.Add((localMod.Title, updated, updated ? "Updated" : "Skipped - update not applied"));
                            }
                            catch (OperationCanceledException)
                            {
                                results.Add((localMod.Title, false, "Cancelled"));
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
                            try { progressReporter.Increment(); } catch (Exception ex) { _logService.LogDebug($"ProgressReporter.Increment failed: {ex.Message}"); }
                            semaphore.Release();
                        }
                    }, token));
                }

                try { await Task.WhenAll(tasks); } catch (OperationCanceledException) { /* propagate to summary */ }

                await _uiService.InvokeAsync(() =>
                {
                    SetStatusInternal("Update run complete. Preparing summary...");
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
            finally
            {
                _isBatchUpdateInProgress = false;
                try { await progressReporter.EndAsync(minimal: true); } catch (Exception ex) { _logService.LogDebug($"progressReporter.EndAsync failed: {ex.Message}"); }
                try { await _uiService.InvokeAsync(() => SetStatusInternal("")); } catch (Exception ex) { _logService.LogDebug($"Clearing status failed: {ex.Message}"); }
            }
        }

        public async Task UpdateAllInternalAsync(CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            var token = linked.Token;

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
                await UpdateModsCoreAsync(modsToUpdate, dependencyHandler, progressReporter, concurrency, token);

                var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in modsToUpdate) affected.Add(m.Name);

                try { await _uiCallbacks.ForceRefreshAffectedModsAsync(affected); } catch (Exception ex) { _logService.LogDebug($"ForceRefreshAffectedModsAsync failed: {ex.Message}"); }

                try { await _uiService.InvokeAsync(() => SetStatusInternal("All updates applied.")); } catch (Exception ex) { _logService.LogDebug($"Clearing pending updates filter failed: {ex.Message}"); }
            }
            catch (OperationCanceledException)
            {
                _logService.LogDebug("UpdateAll cancelled by token");
                await _uiService.InvokeAsync(() => SetStatusInternal("Update cancelled.", LogLevel.Warning));
            }
            catch (Exception ex)
            {
                _logService.LogError($"UpdateAll failed: {ex.Message}", ex);
                await _uiService.InvokeAsync(() => SetStatusInternal($"Update All failed: {ex.Message}", LogLevel.Error));
            }
        }

        private async Task UpdateSingleInternalAsync(ModViewModel mod, CancellationToken cancellationToken = default)
        {
            var dependencyHandler = new SingleDependencyHandler(this);
            var progressReporter = new SingleProgressReporter(this);
            await UpdateModsCoreAsync([mod], dependencyHandler, progressReporter, concurrency: 1, cancellationToken);
        }

        // Internal helper: set status via UI callbacks
        private void SetStatusInternal(string message, LogLevel level = LogLevel.Info)
        {
            try { _uiCallbacks.SetStatusAsync(message, level); } catch (Exception ex) { _logService.LogDebug($"SetStatusInternal failed: {ex.Message}"); }
        }

        // Download/update a single mod (accepts cancellation token)
        private async Task DownloadUpdateInternal(ModViewModel? mod, bool suppressDependencyPrompts = false, CancellationToken cancellationToken = default)
        {
            if (mod == null || !mod.HasUpdate || string.IsNullOrEmpty(mod.LatestVersion))
                return;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            var token = linked.Token;
            token.ThrowIfCancellationRequested();

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

                token.ThrowIfCancellationRequested();

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
                    _logService.LogDebug($"Suppressing dependency prompt for {mod.Name}; attempting quick re-resolve of dependencies.");
                    try
                    {
                        await Task.Delay(200, token);
                        updateResolution = await _dependencyFlow.ResolveForUpdateAsync(mod.Name, mod.LatestVersion!, _allMods, planned);
                    }
                    catch (Exception ex)
                    {
                        _logService.LogDebug($"Re-resolve after suppressed prompt failed for {mod.Name}: {ex.Message}");
                    }

                    if (updateResolution.MissingDependenciesToInstall.Count > 0)
                    {
                        _logService.LogDebug($"After re-resolve, dependencies still missing for {mod.Name}. Proceeding with update; batch handler likely installed deps on disk.");
                        await _uiService.InvokeAsync(() => SetStatusInternal($"Proceeding update for {mod.Title}: dependencies may be installing in background", LogLevel.Info));
                    }
                }

                _logService.Log($"Starting update for {mod.Title} from {mod.Version} to {mod.LatestVersion}");

                await _uiService.InvokeAsync(() =>
                {
                    mod.IsDownloading = true;
                    mod.HasDownloadProgress = false;
                    mod.DownloadStatusText = $"Preparing download for {mod.Title}...";
                    SetStatusInternal($"Downloading update for {mod.Title}...");
                });

                // Begin single-download progress UI if available (skip in batch)
                try
                {
                    if (!_isBatchUpdateInProgress)
                    {
                        await _uiCallbacks.BeginSingleDownloadProgressAsync();
                        singleProgress = true;
                    }
                }
                catch (Exception ex) { _logService.LogDebug($"BeginSingleDownloadProgressAsync failed: {ex.Message}"); }

                token.ThrowIfCancellationRequested();

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

                token.ThrowIfCancellationRequested();

                var result = await DownloadModFromPortalInternal(mod.Name, mod.Title, latestRelease.Version, latestRelease.DownloadUrl, mod, token);
                if (!result.Success || !result.Value) return;

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

                    try
                    {
                        mod.Version = latestRelease.Version;
                        mod.FilePath = newFilePath;
                        mod.SelectedVersion = latestRelease.Version;
                    }
                    catch (Exception ex) { _logService.LogDebug($"Applying new version to VM failed: {ex.Message}"); }

                    SetStatusInternal($"Update complete for {mod.Title}. Refreshing...");
                });

                await Task.Delay(500, token);

                try
                {
                    var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mod.Name };
                    foreach (var d in installedDepsDuringUpdate) affected.Add(d);
                    foreach (var t in toggledModsDuringUpdate) affected.Add(t);
                    affected.ExceptWith(refreshedNamesDuringResolution);

                    if (affected.Count > 0)
                    {
                        try { await _uiCallbacks.ForceRefreshAffectedModsAsync(affected); } catch (Exception ex) { _logService.LogDebug($"Ignored error refreshing affected mods after install: {ex.Message}"); }
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
            catch (OperationCanceledException)
            {
                _logService.LogDebug($"Download/update cancelled for {mod?.Name}");
                await _uiService.InvokeAsync(() =>
                {
                    if (mod != null)
                    {
                        mod.IsDownloading = false;
                        mod.DownloadStatusText = "Cancelled";
                    }
                    SetStatusInternal($"Update cancelled for {mod?.Title}", LogLevel.Warning);
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
                    try { await _uiCallbacks.EndSingleDownloadProgressAsync(false); } catch (Exception ex) { _logService.LogDebug($"EndSingleDownloadProgressAsync failed: {ex.Message}"); }
                }
            }
        }

        // Download helper using injected download service and cancellation token where possible
        private async Task<Result<bool>> DownloadModFromPortalInternal(
            string modName,
            string modTitle,
            string version,
            string downloadUrl,
            ModViewModel? modForProgress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null;

                if (modForProgress != null)
                {
                    var globalReporter = _uiCallbacks.CreateGlobalDownloadProgressReporter();
                    progress = new Progress<(long bytesDownloaded, long? totalBytes)>(p =>
                    {
                        _uiService.Post(() =>
                        {
                            modForProgress.HasDownloadProgress = p.totalBytes.HasValue && p.totalBytes.Value > 0;
                            if (p.totalBytes.HasValue && p.totalBytes.Value > 0)
                            {
                                var progressPercent = (double)p.bytesDownloaded / p.totalBytes.Value * 100;
                                modForProgress.DownloadProgress = progressPercent;
                                modForProgress.DownloadStatusText = $"Downloading... {progressPercent:F0}%";
                            }
                            else
                            {
                                var mbDownloaded = p.bytesDownloaded / 1024.0 / 1024.0;
                                modForProgress.DownloadStatusText = $"Downloading... {mbDownloaded:F2} MB";
                            }
                        });

                        try { globalReporter.Report(p); } catch (Exception ex) { _logService.LogDebug($"Global reporter Report failed: {ex.Message}"); }
                    });
                }
                else
                {
                    progress = _uiCallbacks.CreateGlobalDownloadProgressReporter();
                }

                // Attempt to call download; passing cancellation token to the service (it supports it)
                var result = await _downloadService.DownloadModAsync(modName, modTitle, version, downloadUrl, progress, cancellationToken);

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
            catch (OperationCanceledException)
            {
                return Result<bool>.Fail("Cancelled", ErrorCode.OperationCancelled);
            }
            catch (Exception ex)
            {
                HandleErrorInternal(ex, $"Error downloading {modTitle}: {ex.Message}");
                return Result<bool>.Fail(ex.Message, ErrorCode.DownloadFailed);
            }
        }

        // Internal install logic (accepts cancellation token)
        private async Task<Result> InstallModInternal(string modName, CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            var token = linked.Token;

            var singleProgress = false;

            try
            {
                token.ThrowIfCancellationRequested();

                var modDetails = await _apiService.GetModDetailsAsync(modName);

                if (modDetails?.Releases == null || modDetails.Releases.Count == 0)
                {
                    _logService.LogWarning($"Failed to fetch release details for {modName}");
                    await _uiService.InvokeAsync(() => SetStatusInternal($"Failed to fetch mod details for {modName}", LogLevel.Error));
                    return Result.Fail("No release information found", ErrorCode.ApiRequestFailed);
                }
                if (modDetails != null)
                {
                    modName = modDetails.Name; // use canonical name
                }
                var latestRelease = modDetails?.Releases.OrderByDescending(r => r.ReleasedAt).FirstOrDefault();
                if (latestRelease == null || string.IsNullOrEmpty(latestRelease.DownloadUrl))
                {
                    _logService.LogWarning($"No download URL found for {modName}");
                    await _uiService.InvokeAsync(() => SetStatusInternal($"No download URL available for {modName}", LogLevel.Error));
                    return Result.Fail("No download URL", ErrorCode.ApiRequestFailed);
                }
                await _uiService.InvokeAsync(() => SetStatusInternal($"Downloading {modName}..."));

                try
                {
                    if (!_runInstallFlowActive && !_isBatchUpdateInProgress && !_isBatchDependencyInstallInProgress)
                    {
                        await _uiCallbacks.BeginSingleDownloadProgressAsync();
                        singleProgress = true;
                    }
                }
                catch (Exception ex) { _logService.LogDebug($"BeginSingleDownloadProgressAsync failed in InstallModInternal: {ex.Message}"); }

                token.ThrowIfCancellationRequested();

                var downloadResult = await _downloadService.DownloadModAsync(modName, modDetails?.Title ?? modName, latestRelease.Version, latestRelease.DownloadUrl, null, token);
                if (!downloadResult.Success) return downloadResult;

                return Result.Ok();
            }
            catch (OperationCanceledException)
            {
                return Result.Fail("Cancelled", ErrorCode.OperationCancelled);
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
                    try { await _uiCallbacks.EndSingleDownloadProgressAsync(minimal: true); } catch (Exception ex) { _logService.LogDebug($"EndSingleDownloadProgressAsync failed in InstallModInternal: {ex.Message}"); }
                }
            }
        }

        private void HandleErrorInternal(Exception ex, string context)
        {
            try { _logService.LogError(context, ex); } catch { /* last-resort silence */ }
        }

        /// <summary>
        /// Orchestrates installing a mod with dependency resolution and optional main install action.
        /// </summary>
        public async Task<Result> RunInstallWithDependenciesAsync(string modName, Func<Task<Result>> installMainAsync, ModInfo? localModInfo = null, CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            var token = linked.Token;

            if (string.IsNullOrWhiteSpace(modName))
                return Result.Fail("Invalid mod name", ErrorCode.InvalidInput);

            try
            {
                var resolution = await _dependencyFlow.ResolveForInstallAsync(modName, _allMods, localModInfo);
                if (!resolution.Proceed)
                    return Result.Fail("Installation cancelled by user due to dependencies.", ErrorCode.OperationCancelled);

                var totalToInstall = resolution.MissingDependenciesToInstall.Distinct(StringComparer.OrdinalIgnoreCase).Count() + 1;
                _logService.LogDebug($"RunInstallWithDependencies for {modName}: totalToInstall={totalToInstall}, deps={resolution.MissingDependenciesToInstall.Count}");

                var hasDepencies = totalToInstall > 1;
                if (hasDepencies)
                {
                    try
                    {
                        SetDownloadProgressTotal(totalToInstall);
                        SetDownloadProgressCompleted(0);
                        SetDownloadProgressVisible(true);
                    }
                    catch (Exception ex) { _logService.LogDebug($"Setting aggregated progress failed: {ex.Message}"); }
                }

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
                catch (Exception ex) { _logService.LogDebug($"Applying enable/disable decisions failed: {ex.Message}"); }

                var installedDeps = new List<string>();

                try
                {
                    try
                    {
                        _isBatchDependencyInstallInProgress = hasDepencies;
                        foreach (var dep in resolution.MissingDependenciesToInstall.Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            token.ThrowIfCancellationRequested();

                            if (_allMods.Any(m => m.Name.Equals(dep, StringComparison.OrdinalIgnoreCase)))
                                continue;

                            var depResult = await InstallModInternal(dep, token);
                            if (!depResult.Success)
                            {
                                try { await _uiCallbacks.SetStatusAsync($"Failed to install dependency {dep}: {depResult.Error}", LogLevel.Warning); } catch (Exception ex) { _logService.LogDebug($"Setting status after dep install fail failed: {ex.Message}"); }
                                return Result.Fail(depResult.Error ?? "Dependency install failed", depResult.Code);
                            }

                            installedDeps.Add(dep);

                            if (hasDepencies)
                            {
                                try { IncrementDownloadProgressCompleted(); } catch (Exception ex) { _logService.LogDebug($"IncrementDownloadProgressCompleted failed: {ex.Message}"); }
                                try { HostScheduleBatchedProgressUiUpdate(); } catch (Exception ex) { _logService.LogDebug($"HostScheduleBatchedProgressUiUpdate failed: {ex.Message}"); }
                            }
                        }
                    }
                    finally
                    {
                        _isBatchDependencyInstallInProgress = false;
                    }

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

                    if (hasDepencies)
                    {
                        try { IncrementDownloadProgressCompleted(); } catch (Exception ex) { _logService.LogDebug($"IncrementDownloadProgressCompleted failed after main: {ex.Message}"); }
                        try { HostScheduleBatchedProgressUiUpdate(); } catch (Exception ex) { _logService.LogDebug($"HostScheduleBatchedProgressUiUpdate failed after main: {ex.Message}"); }
                    }

                    var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var d in installedDeps) affected.Add(d);
                    foreach (var e in resolution.ModsToEnable) affected.Add(e.Name);
                    foreach (var d in resolution.ModsToDisable) affected.Add(d.Name);
                    affected.Add(modName);

                    try { await _uiCallbacks.ForceRefreshAffectedModsAsync(affected); } catch (Exception ex) { _logService.LogDebug($"ForceRefreshAffectedModsAsync failed: {ex.Message}"); }

                    try { await _uiCallbacks.SetStatusAsync($"Successfully installed {modName}", LogLevel.Info); } catch (Exception ex) { _logService.LogDebug($"Setting success status failed: {ex.Message}"); }

                    return Result.Ok();
                }
                finally
                {
                    if (hasDepencies)
                    {
                        try { SetDownloadProgressVisible(false); } catch (Exception ex) { _logService.LogDebug($"Hiding progress failed: {ex.Message}"); }
                    }
                    else
                    {
                        try { await _uiCallbacks.EndSingleDownloadProgressAsync(minimal: true); } catch (Exception ex) { _logService.LogDebug($"EndSingleDownloadProgressAsync failed in finally: {ex.Message}"); }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                try { await _uiCallbacks.SetStatusAsync($"Installation cancelled for {modName}", LogLevel.Warning); } catch (Exception ex) { _logService.LogDebug($"Setting cancelled status failed: {ex.Message}"); }
                return Result.Fail("Cancelled", ErrorCode.OperationCancelled);
            }
            catch (Exception ex)
            {
                try { await _uiCallbacks.SetStatusAsync($"Error installing {modName}: {ex.Message}", LogLevel.Error); } catch (Exception logEx) { _logService.LogDebug($"Setting error status failed: {logEx.Message}"); }
                return Result.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        // Progress control methods (forwarded to callbacks)
        public void SetDownloadProgressTotal(int total)
        {
            _currentProgressTotal = total;
            _logService.LogDebug($"SetDownloadProgressTotal: {total}");
            _uiCallbacks.SetDownloadProgressTotal(total);
        }

        public void SetDownloadProgressCompleted(int completed)
        {
            _currentProgressCompleted = completed;
            _logService.LogDebug($"SetDownloadProgressCompleted: {completed}/{_currentProgressTotal}");
            _uiCallbacks.SetDownloadProgressCompleted(completed);
        }

        public void IncrementDownloadProgressCompleted()
        {
            _currentProgressCompleted++;
            _logService.LogDebug($"IncrementDownloadProgressCompleted: {_currentProgressCompleted}/{_currentProgressTotal}");
            _uiCallbacks.IncrementDownloadProgressCompleted();
            try { HostScheduleBatchedProgressUiUpdate(); } catch (Exception ex) { _logService.LogDebug($"HostScheduleBatchedProgressUiUpdate failed: {ex.Message}"); }
        }

        public void SetDownloadProgressVisible(bool visible)
        {
            _logService.LogDebug($"SetDownloadProgressVisible: {visible}");
            _uiCallbacks.SetDownloadProgressVisible(visible);
        }

        public void SetBatchDependencyInstallInProgress(bool inProgress)
        {
            _isBatchDependencyInstallInProgress = inProgress;
            _logService.LogDebug($"SetBatchDependencyInstallInProgress: {inProgress}");
        }
    }
}