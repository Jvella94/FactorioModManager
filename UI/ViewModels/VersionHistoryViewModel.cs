using FactorioModManager.Models.API;
using FactorioModManager.Services;
using FactorioModManager.Services.API;
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
        private readonly IFactorioApiService _apiService;

        public string ModTitle { get; }
        public string ModName { get; }  // ✅ Store mod name

        public ObservableCollection<VersionHistoryReleaseViewModel> Releases { get; }

        public ReactiveCommand<VersionHistoryReleaseViewModel, Unit> ActionCommand { get; }

        public VersionHistoryViewModel(IModService modService, IFactorioApiService apiService,
                                     string modTitle, string modName, List<ModReleaseDto> releases)
        {
            _modService = modService;
            _apiService = apiService;
            ModTitle = modTitle;
            ModName = modName;

            Releases = new ObservableCollection<VersionHistoryReleaseViewModel>(
               releases.OrderByDescending(r => r.ReleasedAt)
               .Select(r => new VersionHistoryReleaseViewModel(r, modService, modName))  // ✅ Pass modName
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
                release.InfoJson = null;

                // ✅ Notify parent mod to refresh count
                NotifyParentModInstalledCountChanged();

                LogService.Instance.Log($"🗑️ Deleted {ModName} v{release.Version}");
            }
            catch (Exception ex)
            {
                LogService.Instance.LogError($"Delete failed: {ex.Message}");
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
                var fullDownloadUrl = $"https://mods.factorio.com{release.DownloadUrl}";
                await _modService.DownloadVersionAsync(ModName, release.Version, fullDownloadUrl);  // ✅ Uses ModName

                // Verify exact file exists now
                var expectedFile = Path.Combine(_modService.GetModsDirectory(), $"{ModName}_{release.Version}.zip");
                if (File.Exists(expectedFile))
                {
                    release.IsInstalled = true;
                    release.InfoJson = release.ExtractInfoJsonFromZip(expectedFile);
                }
                NotifyParentModInstalledCountChanged();

                LogService.Instance.Log($"✅ Installed {ModName} v{release.Version}");
            }
            catch (Exception ex)
            {
                LogService.Instance.LogError($"Download failed: {ex.Message}");
            }
            finally
            {
                release.IsInstalling = false;
            }
        }

        private void NotifyParentModInstalledCountChanged()
        {
            var installedVersions = ServiceContainer.Instance
                 .Resolve<IModService>()
                 .GetInstalledVersions(ModName);
            foreach (var release in Releases)
            {
                release.IsInstalled = installedVersions.Contains(release.Version);
            }

            LogService.Instance.Log($"Updated {Releases.Count} releases for {ModName}");
            // Option 1: Raise event or use messenger service
            // Option 2: Refresh via ModService
            ServiceContainer.Instance.Resolve<IModService>().RefreshInstalledCounts(ModName);
        }
    }
}