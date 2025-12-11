using Avalonia.Controls;
using FactorioModManager.Models.DTO;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Mods;
using FactorioModManager.Services.Settings;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using FactorioModManager.ViewModels.MainWindow;
using System.IO;
using FactorioModManager.Services;

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
            _versionManager = versionManager;
            _settingsService = settingsService;
            _logService = logService;
            _uiService = uiService;
            _modService = modService;
            _parentWindow = parentWindow;
            ModTitle = modTitle;
            ModName = modName;

            Releases = new ObservableCollection<VersionHistoryReleaseViewModel>(
                releases.OrderByDescending(r => r.ReleasedAt)
                    .Select(r => new VersionHistoryReleaseViewModel(r, versionManager, modName))
            );

            // Subscribe to changes in installed status to update CanDelete
            foreach (var release in Releases)
            {
                release.WhenAnyValue(x => x.IsInstalled)
                    .Subscribe(_ => UpdateCanDeleteFlags());
            }

            UpdateCanDeleteFlags();

            // ActionCommand with cancellation support
            ActionCommand = ReactiveCommand.CreateFromTask<VersionHistoryReleaseViewModel>(
                HandleActionAsync);

            CancelCommand = ReactiveCommand.Create(() =>
            {
                _currentOperationCts?.Cancel();
            });
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
                    }
                    catch (OperationCanceledException)
                    {
                        _logService.Log("Deleting Version Cancelled.");
                        throw;
                    }
                    catch (UnauthorizedAccessException uaEx)
                    {
                        // File is locked / permission issue — report to user and stop retrying
                        _logService.LogWarning($"Unauthorized deleting {ModName}_{release.Version}.zip: {uaEx.Message}");
                        await _uiService.ShowMessageAsync(
                            "Delete Failed",
                            $"Unable to delete {ModTitle} version {release.Version}: access denied or file in use. Please close other programs and try again.");
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
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"Delete failed: {ex.Message}", ex);
                        await _uiService.ShowMessageAsync("Delete Failed", $"Unexpected error deleting version: {ex.Message}");
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

                var progress = new Progress<(long bytesDownloaded, long? totalBytes)>(p =>
                {
                    if (p.totalBytes.HasValue && p.totalBytes.Value > 0)
                    {
                        release.DownloadProgress =
                            (p.bytesDownloaded * 100.0) / p.totalBytes.Value;
                    }
                });

                await _versionManager.DownloadVersionAsync(
                    ModName,
                    release.Version,
                    fullDownloadUrl,
                    progress,
                    cancellationToken);

                release.IsInstalled = true;
                _logService.Log($"✅ Installed {ModName} v{release.Version}");
            }
            catch (OperationCanceledException)
            {
                _logService.Log("Download cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Download failed: {ex.Message}", ex);
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
                        var files = Directory.GetFiles(modsDirectory, $"{ModName}_*.zip")
                                             .OrderByDescending(f => f)
                                             .ToList();

                        modVm.VersionFilePaths.Clear();
                        foreach (var f in files)
                        {
                            modVm.VersionFilePaths.Add(f);
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
                _disposables?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}