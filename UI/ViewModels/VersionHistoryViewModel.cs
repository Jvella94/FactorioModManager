using FactorioModManager.Models.DTO;
using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels
{
    public class VersionHistoryViewModel : ReactiveObject
    {
        private readonly IModService _modService;
        private readonly ISettingsService _settingsService;
        private readonly ILogService _logService;

        public string ModTitle { get; }
        public string ModName { get; }  // ✅ Store mod name

        public ObservableCollection<VersionHistoryReleaseViewModel> Releases { get; }

        public ReactiveCommand<VersionHistoryReleaseViewModel, Unit> ActionCommand { get; }

        public VersionHistoryViewModel(IModService modService, ISettingsService settingsService, ILogService logService,
                                     string modTitle, string modName, List<ReleaseDTO> releases)
        {
            _modService = modService;
            _settingsService = settingsService;
            _logService = logService;
            ModTitle = modTitle;
            ModName = modName;

            Releases = new ObservableCollection<VersionHistoryReleaseViewModel>(
               releases.OrderByDescending(r => r.ReleasedAt)
               .Select(r => new VersionHistoryReleaseViewModel(r, modService, modName, logService))  // ✅ Pass modName
           );

            ActionCommand = ReactiveCommand.CreateFromTask<VersionHistoryReleaseViewModel>(HandleActionAsync);
        }

        private async Task HandleActionAsync(VersionHistoryReleaseViewModel release)
        {
            if (release.IsInstalled)
            {
                await DeleteVersionAsync(release);  // Delete installed version
            }
            else
            {
                await DownloadVersionAsync(release);  // Download new version
            }
            NotifyParentModInstalledCountChanged();
        }

        private async Task DeleteVersionAsync(VersionHistoryReleaseViewModel release)
        {
            release.IsInstalling = true;  // Show "Deleting..." progress

            try
            {
                _modService.DeleteVersion(ModName, release.Version);
                release.IsInstalled = false;

                NotifyParentModInstalledCountChanged();

                _logService.Log($"🗑️ Deleted {ModName} v{release.Version}");
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

        private async Task DownloadVersionAsync(VersionHistoryReleaseViewModel release)
        {
            release.IsInstalling = true;
            release.DownloadProgress = 0;

            try
            {
                var downloadUrl = release.DownloadUrl;
                if (downloadUrl == null)
                {
                    _logService.LogWarning("Download url for release not found.");
                    release.IsInstalling = false;
                    return;
                }
                var username = _settingsService.GetUsername();
                var token = _settingsService.GetToken();
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
                {
                    _logService.LogWarning("Download requires username and token from Factorio");
                    release.IsInstalling = false;
                    return;
                }
                var fullDownloadUrl = Constants.Urls.GetModDownloadUrl(downloadUrl, username, token);
                await _modService.DownloadVersionAsync(ModName, release.Version, fullDownloadUrl);  // ✅ Uses ModName

                // Verify exact file exists now
                var expectedFile = Path.Combine(_modService.GetModsDirectory(), $"{ModName}_{release.Version}.zip");
                if (File.Exists(expectedFile))
                {
                    release.IsInstalled = true;
                }
                NotifyParentModInstalledCountChanged();

                _logService.Log($"✅ Installed {ModName} v{release.Version}");
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

        private void NotifyParentModInstalledCountChanged()
        {
            var installedVersions = _modService.GetInstalledVersions(ModName);
            foreach (var release in Releases)
            {
                release.IsInstalled = installedVersions.Contains(release.Version);
            }
            _logService.Log($"Updated {Releases.Count} releases for {ModName}");
            // Option 1: Raise event or use messenger service
            // Option 2: Refresh via ModService
            _modService.RefreshInstalledVersions(ModName);
        }
    }
}