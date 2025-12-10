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

namespace FactorioModManager.ViewModels.Dialogs
{
    public class VersionHistoryViewModel : ViewModelBase
    {
        private readonly IModVersionManager _versionManager;
        private readonly ISettingsService _settingsService;
        private readonly ILogService _logService;
        private readonly IUIService _uiService;
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
            string modTitle,
            string modName,
            List<ShortReleaseDTO> releases,
            Window? parentWindow = null)
        {
            _versionManager = versionManager;
            _settingsService = settingsService;
            _logService = logService;
            _uiService = uiService;
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

            // ✅ ActionCommand with cancellation support
            ActionCommand = ReactiveCommand.CreateFromTask<VersionHistoryReleaseViewModel>(
                HandleActionAsync);

            // ✅ Cancel command
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
                    }
                }
                else
                {
                    await DownloadVersionAsync(releaseVM, _currentOperationCts.Token);
                }

                RefreshInstalledStates();
            }
            catch (OperationCanceledException)
            {
                _logService.Log("Delete/Download cancelled");
            }
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

                // Run deletion on background thread
                await Task.Run(() =>
                    _versionManager.DeleteVersion(ModName, release.Version),
                    cancellationToken);

                release.IsInstalled = false;
                _logService.Log($"🗑️ Deleted {ModName} v{release.Version}");
            }
            catch (OperationCanceledException)
            {
                _logService.Log("Deleting Version Cancelled.");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Delete failed: {ex.Message}", ex);
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