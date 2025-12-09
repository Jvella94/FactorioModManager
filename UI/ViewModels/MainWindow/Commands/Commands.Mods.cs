using FactorioModManager.Models;
using FactorioModManager.Services;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        public ReactiveCommand<Unit, Unit> RefreshModsCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> InstallModCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> OpenModFolderCommand { get; private set; } = null!;
        public ReactiveCommand<ModViewModel, Unit> ToggleModCommand { get; private set; } = null!;
        public ReactiveCommand<ModViewModel, Unit> RemoveModCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> OpenModPortalCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> OpenSourceUrlCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> OpenChangelogCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> OpenVersionHistoryCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> CheckUpdatesCustomCommand { get; private set; } = null!;
        public ReactiveCommand<ModViewModel, Unit> DownloadUpdateCommand { get; private set; } = null!;
        public ReactiveCommand<ModViewModel, Unit> DeleteOldVersionCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> CheckSingleModUpdateCommand { get; private set; } = null!;
        public ReactiveCommand<ModViewModel, Unit> ViewDependentsCommand { get; private set; } = null!;

        private void InitializeModCommands()
        {
            RefreshModsCommand = ReactiveCommand.CreateFromTask(RefreshModsAsync);
            InstallModCommand = ReactiveCommand.CreateFromTask(InstallModAsync);
            OpenModFolderCommand = ReactiveCommand.Create(OpenModFolder);
            ToggleModCommand = ReactiveCommand.Create<ModViewModel>(mod => ToggleMod(mod));
            RemoveModCommand = ReactiveCommand.Create<ModViewModel>(mod => RemoveMod(mod));
            OpenModPortalCommand = ReactiveCommand.Create(OpenModPortal);
            OpenSourceUrlCommand = ReactiveCommand.Create(OpenSourceUrl);
            OpenChangelogCommand = ReactiveCommand.CreateFromTask(OpenChangelogAsync);
            OpenVersionHistoryCommand = ReactiveCommand.CreateFromTask(OpenVersionHistoryAsync);
            CheckUpdatesCustomCommand = ReactiveCommand.CreateFromTask(CheckUpdatesCustomAsync);
            DownloadUpdateCommand = ReactiveCommand.CreateFromTask<ModViewModel>(mod => DownloadUpdateAsync(mod));
            DeleteOldVersionCommand = ReactiveCommand.Create<ModViewModel>(mod => DeleteOldVersion(mod));
            CheckSingleModUpdateCommand = ReactiveCommand.CreateFromTask(CheckSingleModUpdateAsync);
            ViewDependentsCommand = ReactiveCommand.CreateFromTask<ModViewModel>(ViewDependentsAsync);
        }

        /// <summary>
        /// Opens the mods folder in file explorer
        /// </summary>
        private void OpenModFolder()
        {
            try
            {
                var path = _modService.GetModsDirectory();
                _uiService.OpenFolder(path);
                SetStatus($"Opened: {path}");
            }
            catch (Exception ex)
            {
                HandleError(ex, $"Error opening mod folder");
            }
        }

        /// <summary>
        /// Opens the selected mod's page on the mod portal
        /// </summary>
        private void OpenModPortal()
        {
            if (SelectedMod == null)
            {
                SetStatus("No mod selected", LogLevel.Warning);
                return;
            }

            try
            {
                var url = Constants.Urls.GetModUrl(SelectedMod.Name);
                _uiService.OpenUrl(url);
                SetStatus($"Opened mod portal for {SelectedMod.Title}");
            }
            catch (Exception ex)
            {
                HandleError(ex, "Error opening mod portal.");
            }
        }

        /// <summary>
        /// Opens the selected mod's source URL
        /// </summary>
        private void OpenSourceUrl()
        {
            if (SelectedMod == null)
            {
                SetStatus("No mod selected", LogLevel.Warning);
                return;
            }

            if (string.IsNullOrEmpty(SelectedMod.SourceUrl))
            {
                SetStatus("No source URL available for this mod", LogLevel.Warning);
                return;
            }

            try
            {
                _uiService.OpenUrl(SelectedMod.SourceUrl);
                SetStatus($"Opened source URL for {SelectedMod.Title}");
            }
            catch (Exception ex)
            {
                HandleError(ex, "Error opening source url");
            }
        }

        /// <summary>
        /// Shows custom update check dialog
        /// </summary>
        private async Task CheckUpdatesCustomAsync()
        {
            var (success, hours) = await _uiService.ShowUpdateCheckDialogAsync();

            if (success)
            {
                await CheckForUpdatesAsync(hours);
            }
        }

        /// <summary>
        /// Shows the install mod dialog
        /// </summary>
        private async Task InstallModAsync()
        {
            var (success, data, isUrl) = await _uiService.ShowInstallModDialogAsync();

            if (success && data != null)
            {
                var installResult = isUrl
                    ? await InstallModFromUrlAsync(data)
                    : await InstallModFromFileAsync(data);

                if (installResult.Success)
                {
                    await RefreshModsAsync();
                }
                else if (installResult.Error != null)
                {
                    SetStatus(installResult.Error, LogLevel.Error);
                }
            }
        }

        /// <summary>
        /// Installs a mod from a local file
        /// </summary>
        private async Task<Models.Result<bool>> InstallModFromFileAsync(string filePath)
        {
            return await Task.Run(async () =>
            {
                return await _downloadService.InstallFromLocalFileAsync(filePath);
            });
        }

        /// <summary>
        /// Installs a mod from a mod portal URL
        /// </summary>
        private async Task<Models.Result<bool>> InstallModFromUrlAsync(string url)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    var modName = url.Split('/').LastOrDefault();
                    if (string.IsNullOrEmpty(modName))
                    {
                        await _uiService.InvokeAsync(() =>
                        {
                            SetStatus("Invalid mod portal URL", LogLevel.Error);
                        });
                        return Models.Result<bool>.Fail("Invalid URL format", Models.ErrorCode.InvalidInput);
                    }

                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatus($"Fetching {modName} from mod portal...");
                    });

                    var modDetails = await _apiService.GetModDetailsAsync(modName);

                    if (modDetails?.Releases == null || modDetails.Releases.Count == 0)
                    {
                        _logService.LogWarning($"Failed to fetch release details for {modName}");
                        await _uiService.InvokeAsync(() =>
                        {
                            SetStatus($"Failed to fetch mod details for {modName}", LogLevel.Error);
                        });
                        return Models.Result<bool>.Fail("No release information found",
                            Models.ErrorCode.ApiRequestFailed);
                    }

                    var latestRelease = modDetails.Releases
                        .OrderByDescending(r => r.ReleasedAt)
                        .FirstOrDefault();

                    if (latestRelease == null || string.IsNullOrEmpty(latestRelease.DownloadUrl))
                    {
                        _logService.LogWarning($"No download URL found for {modName}");
                        await _uiService.InvokeAsync(() =>
                        {
                            SetStatus($"No download URL available for {modName}", LogLevel.Error);
                        });
                        return Models.Result<bool>.Fail("No download URL", Models.ErrorCode.ApiRequestFailed);
                    }

                    var modTitle = modDetails.Title ?? modName;

                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatus($"Downloading {modTitle}...");
                    });

                    var downloadResult = await _downloadService.DownloadModAsync(
                        modName,
                        modTitle,
                        latestRelease.Version,
                        latestRelease.DownloadUrl
                    );

                    if (downloadResult.Success)
                    {
                        await _uiService.InvokeAsync(() =>
                        {
                            SetStatus($"Successfully installed {modTitle}. Refreshing...");
                        });
                        _logService.Log($"Successfully installed {modTitle} version {latestRelease.Version}");
                    }

                    return downloadResult;
                }
                catch (Exception ex)
                {
                    await _uiService.InvokeAsync(() =>
                    {
                        HandleError(ex, $"Error installing mod from url {url}");
                    });
                    return Result<bool>.Fail(ex.Message, ErrorCode.UnexpectedError);
                }
            });
        }
    }
}