using FactorioModManager.Models.DTO;
using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using ReactiveUI;
using System;
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
        private readonly IModService _modService;
        private readonly ISettingsService _settingsService;
        private readonly ILogService _logService;
        private readonly CompositeDisposable _disposables = [];
        private CancellationTokenSource? _currentOperationCts;

        public string ModTitle { get; }
        public string ModName { get; }
        public ObservableCollection<VersionHistoryReleaseViewModel> Releases { get; }

        public ReactiveCommand<VersionHistoryReleaseViewModel, Unit> ActionCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public VersionHistoryViewModel(
            IModService modService,
            ISettingsService settingsService,
            ILogService logService,
            string modTitle,
            string modName,
            System.Collections.Generic.List<ReleaseDTO> releases)
        {
            _modService = modService;
            _settingsService = settingsService;
            _logService = logService;
            ModTitle = modTitle;
            ModName = modName;

            Releases = new ObservableCollection<VersionHistoryReleaseViewModel>(
                releases.OrderByDescending(r => r.ReleasedAt)
                    .Select(r => new VersionHistoryReleaseViewModel(r, modService, modName))
            );

            // ✅ ActionCommand with cancellation support
            ActionCommand = ReactiveCommand.CreateFromTask<VersionHistoryReleaseViewModel>(
                HandleActionAsync);

            // ✅ Cancel command
            CancelCommand = ReactiveCommand.Create(() =>
            {
                _currentOperationCts?.Cancel();
            });
        }

        private async Task HandleActionAsync(VersionHistoryReleaseViewModel release)
        {
            // Cancel any ongoing operation
            _currentOperationCts?.Cancel();
            _currentOperationCts = new CancellationTokenSource();

            try
            {
                if (release.IsInstalled)
                {
                    await DeleteVersionAsync(release, _currentOperationCts.Token);
                }
                else
                {
                    await DownloadVersionAsync(release, _currentOperationCts.Token);
                }

                RefreshInstalledStates();
            }
            catch (OperationCanceledException)
            {
                _logService.Log("Operation cancelled");
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
                    _modService.DeleteVersion(ModName, release.Version),
                    cancellationToken);

                release.IsInstalled = false;
                _logService.Log($"🗑️ Deleted {ModName} v{release.Version}");
            }
            catch (OperationCanceledException)
            {
                throw;
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

                await _modService.DownloadVersionAsync(
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
            var installedVersions = _modService.GetInstalledVersions(ModName);

            foreach (var release in Releases)
            {
                release.IsInstalled = installedVersions.Contains(release.Version);
            }

            _modService.RefreshInstalledVersions(ModName);
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