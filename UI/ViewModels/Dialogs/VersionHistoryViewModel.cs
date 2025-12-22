using Avalonia.Controls;
using FactorioModManager.Models.DTO;
using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Mods;
using FactorioModManager.Services.Settings;
using FactorioModManager.ViewModels.MainWindow;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.Dialogs
{
    public class VersionHistoryViewModel : ViewModelBase
    {
        private readonly IModVersionManager _versionManager;
        private readonly ISettingsService _settingsService;
        private readonly ILogService _logService;
        private readonly IUIService _uiService;
        private readonly IModService _modService;
        private readonly Window? _parentWindow;
        private readonly CompositeDisposable _disposables = [];
        private CancellationTokenSource? _currentOperationCts;

        // Indicates whether any release is currently performing an operation
        private bool _isOperationInProgress;

        public bool IsOperationInProgress
        {
            get => _isOperationInProgress;
            private set => this.RaiseAndSetIfChanged(ref _isOperationInProgress, value);
        }

        // Called by the view when a close attempt is blocked; shows user message
        public Task NotifyCloseBlockedAsync()
        {
            return _uiService.ShowMessageAsync("Operation in progress", "A download or deletion is in progress. Please cancel it before closing this window.", _parentWindow);
        }

        public string ModTitle { get; }
        public string ModName { get; }
        public ObservableCollection<VersionHistoryReleaseViewModel> Releases { get; }
        public ReactiveCommand<VersionHistoryReleaseViewModel, Unit> ActionCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public VersionHistoryViewModel(
            IModVersionManager versionManager,
            ISettingsService settingsService,
            ILogService logService,
            IUIService uiService,
            IModService modService,
            string modTitle,
            string modName,
            List<ShortReleaseDTO> releases,
            Window? parentWindow = null)
        {
            _versionManager = versionManager ?? throw new ArgumentNullException(nameof(versionManager));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
            _modService = modService ?? throw new ArgumentNullException(nameof(modService));
            _parentWindow = parentWindow;
            ModTitle = modTitle;
            ModName = modName;

            Releases = new ObservableCollection<VersionHistoryReleaseViewModel>(
                releases.OrderByDescending(r => r.ReleasedAt)
                    .Select(r => new VersionHistoryReleaseViewModel(r, versionManager, modName))
            );

            // Subscribe to changes in installed status to update CanDelete and track IsInstalling
            foreach (var release in Releases)
            {
                var d1 = release.WhenAnyValue(x => x.IsInstalled)
                    .Subscribe(_ => UpdateCanDeleteFlags());
                _disposables.Add(d1);

                var d2 = release.WhenAnyValue(x => x.IsInstalling)
                    .Subscribe(_ => UpdateOperationFlag());
                _disposables.Add(d2);
            }

            UpdateCanDeleteFlags();
            UpdateOperationFlag();

            // ActionCommand with cancellation support
            ActionCommand = ReactiveCommand.CreateFromTask<VersionHistoryReleaseViewModel>(
                HandleActionAsync);

            CancelCommand = ReactiveCommand.Create(() =>
            {
                _currentOperationCts?.Cancel();
            });
        }

        private void UpdateOperationFlag()
        {
            IsOperationInProgress = Releases.Any(r => r.IsInstalling);
        }

        private async Task HandleActionAsync(VersionHistoryReleaseViewModel releaseVM)
        {
            // Cancel any ongoing operation
            _currentOperationCts?.Cancel();
            _currentOperationCts = new CancellationTokenSource();

            try
            {
                if (releaseVM.IsInstalled)
                {
                    // Show confirmation dialog before deletion using UIService
                    var result = await _uiService.ShowConfirmationAsync(
                        "Confirm Deletion",
                        $"Are you sure you want to delete version {releaseVM.Version} of {ModTitle}?",
                        _parentWindow,
                        yesButtonText: "Yes, Delete",
                        noButtonText: "No",
                        yesButtonColor: "#D32F2F",  // Red for destructive action
                        noButtonColor: "#3A3A3A"    // Gray for cancel
                    );

                    if (result)
                    {
                        await DeleteVersionAsync(releaseVM, _currentOperationCts.Token);
                        await CheckforSingleRelease();
                    }
                }
                else
                {
                    await DownloadVersionAsync(releaseVM, _currentOperationCts.Token);
                }

                RefreshInstalledStates();

                // Update mod data in main window so version dropdown reflects changes immediately
                await UpdateMainWindowModVersionsAsync();
            }
            catch (OperationCanceledException)
            {
                _logService.Log("Delete/Download cancelled");
                // Also update UI main status
                await _uiService.InvokeAsync(() =>
                {
                    var mainWin = _uiService.GetMainWindow();
                    if (mainWin?.DataContext is MainWindowViewModel mainVm)
                        mainVm.StatusText = "Operation cancelled";
                });
            }
        }

        private async Task CheckforSingleRelease()
        {
            // If deletion left a single installed version, persist it as active
            var installed = _versionManager.GetInstalledVersions(ModName);
            if (installed.Count == 1)
            {
                try
                {
                    var remaining = installed.First();
                    // Persist no listed version (enable stays true)
                    _modService.SaveModState(ModName, enabled: true, version: null);

                    // Ensure main window reflects active version
                    await _uiService.InvokeAsync(async () =>
                    {
                        var mainWindow = _uiService.GetMainWindow();
                        if (mainWindow?.DataContext is MainWindowViewModel mainVm)
                        {
                            var modVm = mainVm.SelectedMod ?? mainVm.FilteredMods.FirstOrDefault(m => string.Equals(m.Name, ModName, StringComparison.OrdinalIgnoreCase));
                            if (modVm != null)
                            {
                                modVm.SelectedVersion = remaining;
                                modVm.Version = remaining;

                                // Resolve file path: prefer known VersionFilePaths, otherwise construct expected zip name
                                string? filePath = GetFilePathofVersion(remaining, modVm);

                                if (!string.IsNullOrEmpty(filePath))
                                {
                                    modVm.FilePath = filePath;
                                }

                                // Trigger property updates
                                modVm.RaisePropertyChanged(nameof(modVm.HasMultipleVersions));
                            }
                        }
                    });
                }
                catch (Exception exPersist)
                {
                    _logService.LogWarning($"Failed to persist remaining active version for {ModName}: {exPersist.Message}");
                }
            }
        }

        private string? GetFilePathofVersion(string remaining, ModViewModel modVm)
        {
            string? filePath = null;
            var idx = modVm.AvailableVersions.IndexOf(remaining);
            if (idx >= 0 && idx < modVm.VersionFilePaths.Count)
            {
                filePath = modVm.VersionFilePaths[idx];
            }
            else
            {
                var modsDirectory = FolderPathHelper.GetModsDirectory();
                var expected = Path.Combine(modsDirectory, $"{ModName}_{remaining}.zip");
                if (File.Exists(expected))
                {
                    filePath = expected;
                }
                else
                {
                    // try to find any matching file name containing the version substring
                    try
                    {
                        var files = Directory.GetFiles(FolderPathHelper.GetModsDirectory(), $"{ModName}_*.zip");
                        filePath = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).EndsWith($"_{remaining}", StringComparison.OrdinalIgnoreCase));
                    }
                    catch
                    {
                        filePath = null;
                    }
                }
            }
            return filePath;
        }

        private void UpdateCanDeleteFlags()
        {
            var installedCount = Releases.Count(r => r.IsInstalled);
            foreach (var release in Releases)
            {
                release.CanDelete = installedCount > 1;
            }
        }

        private async Task DeleteVersionAsync(
          VersionHistoryReleaseViewModel release,
          CancellationToken cancellationToken)
        {
            release.IsInstalling = true;
            try
            {
                // Update main status and log
                await _uiService.InvokeAsync(() =>
                {
                    var mainWin = _uiService.GetMainWindow();
                    if (mainWin?.DataContext is MainWindowViewModel mainVm)
                        mainVm.StatusText = $"Deleting {ModTitle} version {release.Version}...";
                });
                _logService.Log($"Deleting {ModName} v{release.Version}...");

                cancellationToken.ThrowIfCancellationRequested();

                const int maxAttempts = 3;
                var attempt = 0;
                var deleted = false;

                while (attempt < maxAttempts && !deleted)
                {
                    attempt++;
                    try
                    {
                        // run deletion on background thread (delegate to manager)
                        await Task.Run(() =>
                            _versionManager.DeleteVersion(ModName, release.Version),
                            cancellationToken);

                        deleted = true;
                        release.IsInstalled = false;
                        _logService.Log($"🗑️ Deleted {ModName} v{release.Version}");

                        // Update main status
                        await _uiService.InvokeAsync(() =>
                        {
                            var mainWin = _uiService.GetMainWindow();
                            if (mainWin?.DataContext is MainWindowViewModel mainVm)
                                mainVm.StatusText = $"Deleted {ModTitle} version {release.Version}";
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        _logService.Log("Deleting Version Cancelled.");
                        // Reflect cancellation in status
                        await _uiService.InvokeAsync(() =>
                        {
                            var mainWin = _uiService.GetMainWindow();
                            if (mainWin?.DataContext is MainWindowViewModel mainVm)
                                mainVm.StatusText = "Delete cancelled";
                        });
                        throw;
                    }
                    catch (UnauthorizedAccessException uaEx)
                    {
                        // File is locked / permission issue — report to user and stop retrying
                        _logService.LogWarning($"Unauthorized deleting {ModName}_{release.Version}.zip: {uaEx.Message}");
                        await _uiService.ShowMessageAsync(
                            "Delete Failed",
                            $"Unable to delete {ModTitle} version {release.Version}: access denied or file in use. Please close other programs and try again.");

                        // Update status
                        await _uiService.InvokeAsync(() =>
                        {
                            var mainWin = _uiService.GetMainWindow();
                            if (mainWin?.DataContext is MainWindowViewModel mainVm)
                                mainVm.StatusText = $"Delete failed: access denied for {ModTitle} {release.Version}";
                        });
                        break;
                    }
                    catch (IOException ioEx) when (attempt < maxAttempts)
                    {
                        // Transient IO (file locked momentarily). Retry with small backoff.
                        _logService.LogWarning($"I/O error deleting (attempt {attempt}) {ModName}_{release.Version}.zip: {ioEx.Message}");
                        await Task.Delay(200 * attempt, cancellationToken);
                        continue;
                    }
                    catch (IOException ioEx)
                    {
                        _logService.LogError($"I/O error deleting version: {ioEx.Message}", ioEx);
                        await _uiService.ShowMessageAsync("Delete Failed", $"Failed to delete file: {ioEx.Message}");

                        // Update status
                        await _uiService.InvokeAsync(() =>
                        {
                            var mainWin = _uiService.GetMainWindow();
                            if (mainWin?.DataContext is MainWindowViewModel mainVm)
                                mainVm.StatusText = $"Delete failed: {ioEx.Message}";
                        });
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"Delete failed: {ex.Message}", ex);
                        await _uiService.ShowMessageAsync("Delete Failed", $"Unexpected error deleting version: {ex.Message}");

                        await _uiService.InvokeAsync(() =>
                        {
                            var mainWin = _uiService.GetMainWindow();
                            if (mainWin?.DataContext is MainWindowViewModel mainVm)
                                mainVm.StatusText = $"Delete failed: unexpected error";
                        });
                        break;
                    }
                }

                if (!deleted)
                {
                    // Ensure UI reflects actual state from disk if delete didn't happen
                    RefreshInstalledStates();
                }
            }
            finally
            {
                release.IsInstalling = false;
            }
        }

        private async Task DownloadVersionAsync(
            VersionHistoryReleaseViewModel release,
            CancellationToken cancellationToken)
        {
            release.IsInstalling = true;
            release.DownloadProgress = 0;

            try
            {
                // Update status when starting
                await _uiService.InvokeAsync(() =>
                {
                    var mainWin = _uiService.GetMainWindow();
                    if (mainWin?.DataContext is MainWindowViewModel mainVm)
                        mainVm.StatusText = $"Downloading {ModTitle} v{release.Version}...";
                });
                _logService.Log($"Downloading {ModName} v{release.Version}...");

                var downloadUrl = release.DownloadUrl;
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    _logService.LogWarning("Download URL not found");
                    return;
                }

                var username = _settingsService.GetUsername();
                var token = _settingsService.GetToken();
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
                {
                    _logService.LogWarning("Download requires username and token");
                    return;
                }

                var fullDownloadUrl = Constants.Urls.GetModDownloadUrl(
                    downloadUrl, username, token);

                // Local progress for the dialog
                var localProgress = new Progress<(long bytesDownloaded, long? totalBytes)>(p =>
                {
                    if (p.totalBytes.HasValue && p.totalBytes.Value > 0)
                    {
                        release.DownloadProgress =
                            (p.bytesDownloaded * 100.0) / p.totalBytes.Value;
                    }
                });

                // Try to obtain the main window VM to also report global speed
                IProgress<(long bytesDownloaded, long? totalBytes)>? globalProgress = null;
                MainWindowViewModel? mainVm = null;
                try
                {
                    var mainWindow = _uiService.GetMainWindow();
                    if (mainWindow?.DataContext is MainWindowViewModel mm)
                    {
                        mainVm = mm;
                        // Ensure the main window shows the global download UI and is initialized for a single download
                        await _uiService.InvokeAsync(() =>
                        {
                            try
                            {
                                mainVm.DownloadProgressTotal = 1;
                                mainVm.DownloadProgressCompleted = 0;
                                mainVm.IsDownloadProgressVisible = true;
                            }
                            catch { }
                        });

                        globalProgress = mainVm.CreateGlobalDownloadProgressReporter();
                    }
                }
                catch { }

                // Combined progress forwards to both local and global reporters
                IProgress<(long bytesDownloaded, long? totalBytes)>? combined = null;
                if (globalProgress != null)
                {
                    combined = new Progress<(long bytesDownloaded, long? totalBytes)>(p =>
                    {
                        (localProgress as IProgress<(long, long?)>)?.Report(p);
                        globalProgress.Report(p);
                    });
                }
                else
                {
                    combined = localProgress;
                }

                await _versionManager.DownloadVersionAsync(
                    ModName,
                    release.Version,
                    fullDownloadUrl,
                    combined,
                    cancellationToken);

                release.IsInstalled = true;
                _logService.Log($"✅ Installed {ModName} v{release.Version}");

                // If main VM progress UI was enabled, mark completion and hide UI after a short delay
                if (mainVm != null)
                {
                    try
                    {
                        await _uiService.InvokeAsync(() =>
                        {
                            // increment completed
                            mainVm.DownloadProgressCompleted = mainVm.DownloadProgressCompleted + 1;
                        });

                        // Allow UI animation to reach 100%
                        await Task.Delay(300, cancellationToken);

                        await _uiService.InvokeAsync(() =>
                        {
                            try { mainVm.DownloadProgress.UpdateSpeedText(null); } catch { }
                            try { mainVm.DownloadProgress.UpdateProgressText(string.Empty); } catch { }
                            mainVm.IsDownloadProgressVisible = false;
                            mainVm.DownloadProgressTotal = 0;
                            mainVm.DownloadProgressCompleted = 0;
                            try { mainVm.DownloadProgress.UpdateProgressPercent(0.0); } catch { }
                        });
                    }
                    catch { }
                }

                // Update main status on success
                await _uiService.InvokeAsync(() =>
                {
                    var mainWin = _uiService.GetMainWindow();
                    if (mainWin?.DataContext is MainWindowViewModel m)
                        m.StatusText = $"Installed {ModTitle} v{release.Version}";
                });
            }
            catch (OperationCanceledException)
            {
                _logService.Log("Download cancelled");
                await _uiService.InvokeAsync(() =>
                {
                    var mainWin = _uiService.GetMainWindow();
                    if (mainWin?.DataContext is MainWindowViewModel mainVm)
                        mainVm.StatusText = "Download cancelled";
                });
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Download failed: {ex.Message}", ex);
                await _uiService.InvokeAsync(() =>
                {
                    var mainWin = _uiService.GetMainWindow();
                    if (mainWin?.DataContext is MainWindowViewModel mainVm)
                        mainVm.StatusText = $"Download failed: {ex.Message}";
                });
            }
            finally
            {
                release.IsInstalling = false;
            }
        }

        private void RefreshInstalledStates()
        {
            var installedVersions = _versionManager.GetInstalledVersions(ModName);
            foreach (var release in Releases)
            {
                release.IsInstalled = installedVersions.Contains(release.Version);
            }

            _versionManager.RefreshVersionCache(ModName);
        }

        /// <summary>
        /// Updates the corresponding ModViewModel in the main window (if available)
        /// so the AvailableVersions, VersionFilePaths, InstalledCount and SelectedVersion reflect changes
        /// made here. Best-effort: updates SelectedMod first then falls back to filtered list.
        /// </summary>
        private async Task UpdateMainWindowModVersionsAsync()
        {
            try
            {
                // Execute on UI thread to safely update view models bound to the UI
                await _uiService.InvokeAsync(() =>
                {
                    var mainWindow = _uiService.GetMainWindow();
                    if (mainWindow?.DataContext is not MainWindowViewModel mainVm)
                        return;

                    // Prefer selected mod, then search filtered mods
                    ModViewModel? modVm = null;
                    if (mainVm.SelectedMod != null && string.Equals(mainVm.SelectedMod.Name, ModName, StringComparison.OrdinalIgnoreCase))
                    {
                        modVm = mainVm.SelectedMod;
                    }
                    else
                    {
                        modVm = mainVm.FilteredMods.FirstOrDefault(m => string.Equals(m.Name, ModName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (modVm == null)
                        return;

                    var installed = _versionManager.GetInstalledVersions(ModName);

                    modVm.AvailableVersions.Clear();
                    foreach (var v in installed)
                    {
                        modVm.AvailableVersions.Add(v);
                    }

                    // Also rebuild VersionFilePaths so indexes match AvailableVersions
                    try
                    {
                        var modsDirectory = FolderPathHelper.GetModsDirectory();

                        // Rebuild VersionFilePaths to align with AvailableVersions order.
                        modVm.VersionFilePaths.Clear();
                        foreach (var v in modVm.AvailableVersions)
                        {
                            // Prefer zip file if present
                            var zipCandidate = Path.Combine(modsDirectory, $"{ModName}_{v}.zip");
                            if (File.Exists(zipCandidate))
                            {
                                modVm.VersionFilePaths.Add(zipCandidate);
                                continue;
                            }

                            // Conventional directory name
                            var dirCandidate = Path.Combine(modsDirectory, $"{ModName}_{v}");
                            if (Directory.Exists(dirCandidate))
                            {
                                modVm.VersionFilePaths.Add(dirCandidate);
                                continue;
                            }

                            // Fallback: scan directories and inspect info.json to find matching version
                            try
                            {
                                string? foundDir = null;
                                var dirs = Directory.GetDirectories(modsDirectory);
                                foreach (var d in dirs)
                                {
                                    try
                                    {
                                        var infoPath = Path.Combine(d, Constants.FileSystem.InfoJsonFileName);
                                        if (!File.Exists(infoPath))
                                            continue;
                                        var json = File.ReadAllText(infoPath);
                                        var info = System.Text.Json.JsonSerializer.Deserialize<Models.ModInfo>(json, Constants.JsonOptions.CaseInsensitive);
                                        if (info != null && string.Equals(info.Name, ModName, StringComparison.OrdinalIgnoreCase) && string.Equals(info.Version, v, StringComparison.OrdinalIgnoreCase))
                                        {
                                            foundDir = d;
                                            break;
                                        }
                                    }
                                    catch { }
                                }

                                if (foundDir != null)
                                {
                                    modVm.VersionFilePaths.Add(foundDir);
                                    continue;
                                }
                            }
                            catch { }

                            // Final fallback: try to find any zip that ends with _{version}
                            try
                            {
                                var zips = Directory.GetFiles(modsDirectory, $"{ModName}_*.zip");
                                var match = zips.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).EndsWith($"_{v}", StringComparison.OrdinalIgnoreCase));
                                if (match != null)
                                {
                                    modVm.VersionFilePaths.Add(match);
                                    continue;
                                }
                            }
                            catch { }

                            // Nothing found -> keep index alignment with empty string
                            modVm.VersionFilePaths.Add(string.Empty);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogWarning($"Failed to rebuild version file paths for {ModName}: {ex.Message}");
                    }

                    modVm.InstalledCount = modVm.AvailableVersions.Count;

                    // Ensure SelectedVersion points to an installed version
                    if (!string.IsNullOrEmpty(modVm.Version) && modVm.AvailableVersions.Contains(modVm.Version))
                    {
                        modVm.SelectedVersion = modVm.Version;
                    }
                    else if (modVm.AvailableVersions.Count > 0)
                    {
                        modVm.SelectedVersion = modVm.AvailableVersions.First();
                    }
                    else
                    {
                        modVm.SelectedVersion = null;
                    }

                    modVm.RaisePropertyChanged(nameof(modVm.HasMultipleVersions));
                });
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to update main window mod versions: {ex.Message}", ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _currentOperationCts?.Cancel();
                _currentOperationCts?.Dispose();
                _disposables.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}