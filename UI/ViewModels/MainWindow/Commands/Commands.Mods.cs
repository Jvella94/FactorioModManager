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
            InstallModCommand = ReactiveCommand.Create(InstallMod);
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
            ViewDependentsCommand = ReactiveCommand.CreateFromTask<ModViewModel?>(ViewDependentsAsync);

        }

        private void OpenModFolder()
        {
            _uiService.Post(() =>
            {
                try
                {
                    var path = ModPathHelper.GetModsDirectory();
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                    StatusText = $"Opened: {path}";
                }
                catch (Exception ex)
                {
                    StatusText = $"Error opening folder: {ex.Message}";
                }
            });
        }

        private void OpenModPortal()
        {
            if (SelectedMod != null)
            {
                try
                {
                    var url = Constants.Urls.GetModUrl(SelectedMod.Name);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                    StatusText = $"Opened mod portal for {SelectedMod.Title}";
                }
                catch (Exception ex)
                {
                    StatusText = $"Error opening browser: {ex.Message}";
                }
            }
        }

        private void OpenSourceUrl()
        {
            if (SelectedMod != null && !string.IsNullOrEmpty(SelectedMod.SourceUrl))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = SelectedMod.SourceUrl,
                        UseShellExecute = true
                    });
                    StatusText = $"Opened source URL for {SelectedMod.Title}";
                }
                catch (Exception ex)
                {
                    StatusText = $"Error opening browser: {ex.Message}";
                }
            }
        }

        private async Task CheckUpdatesCustomAsync()
        {
            await Task.Run(() =>
            {
                _uiService.InvokeAsync(async () =>
                {
                    var dialog = new Views.UpdateCheckDialog();
                    var owner = Avalonia.Application.Current?.ApplicationLifetime
                        is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow : null;

                    if (owner != null)
                    {
                        var (Success, Hours) = await dialog.ShowDialog(owner);
                        if (Success)
                        {
                            await CheckForUpdatesAsync(Hours);
                        }
                    }
                });
            });
        }

        private async void InstallMod()
        {
            await _uiService.InvokeAsync(async () =>
            {
                var dialog = new Views.InstallModDialog();
                var owner = Avalonia.Application.Current?.ApplicationLifetime
                    is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow : null;

                if (owner != null)
                {
                    var (Success, Data, IsUrl) = await dialog.ShowDialog(owner);
                    if (Success && Data != null)
                    {
                        var installResult = IsUrl
                            ? await InstallModFromUrlAsync(Data)
                            : await InstallModFromFileAsync(Data);

                        if (installResult.Success)
                        {
                            await RefreshModsAsync();
                        }
                        else if (installResult.Error != null)
                        {
                            StatusText = installResult.Error;
                        }
                    }
                }
            });
        }

        private async Task<Models.Result<bool>> InstallModFromFileAsync(string filePath)
        {
            return await Task.Run(async () =>
            {
                return await InstallModFromLocalFileAsync(filePath);
            });
        }

        private async Task<Models.Result<bool>> InstallModFromUrlAsync(string url)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    var modName = url.Split('/').LastOrDefault();
                    if (string.IsNullOrEmpty(modName))
                    {
                        _uiService.Post(() =>
                        {
                            StatusText = "Invalid mod portal URL";
                        });
                        return Models.Result<bool>.Fail("Invalid URL format", Models.ErrorCode.InvalidInput);
                    }

                    _uiService.Post(() =>
                    {
                        StatusText = $"Fetching {modName} from mod portal...";
                    });

                    var apiKey = _settingsService.GetApiKey();
                    var modDetails = await _apiService.GetModDetailsAsync(modName);

                    if (modDetails?.Releases == null || modDetails.Releases.Count == 0)
                    {
                        _logService.LogError($"Failed to fetch release details for {modName}");
                        _uiService.Post(() =>
                        {
                            StatusText = $"Failed to fetch mod details for {modName}";
                        });
                        return Models.Result<bool>.Fail("No release information found",
                            Models.ErrorCode.ApiRequestFailed);
                    }

                    var latestRelease = modDetails.Releases
                        .OrderByDescending(r => r.ReleasedAt)
                        .FirstOrDefault();

                    if (latestRelease == null || string.IsNullOrEmpty(latestRelease.DownloadUrl))
                    {
                        _logService.LogError($"No download URL found for {modName}");
                        _uiService.Post(() =>
                        {
                            StatusText = $"No download URL available for {modName}";
                        });
                        return Models.Result<bool>.Fail("No download URL", Models.ErrorCode.ApiRequestFailed);
                    }

                    var modTitle = modDetails.Title ?? modName;
                    _uiService.Post(() =>
                    {
                        StatusText = $"Downloading {modTitle}...";
                    });

                    var downloadResult = await DownloadModFromPortalAsync(
                        modName,
                        modTitle,
                        latestRelease.Version,
                        latestRelease.DownloadUrl
                    );

                    if (downloadResult.Success)
                    {
                        _uiService.Post(() =>
                        {
                            StatusText = $"Successfully installed {modTitle}. Refreshing...";
                        });
                        _logService.Log($"Successfully installed {modTitle} version {latestRelease.Version}");
                    }

                    return downloadResult;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error installing mod from URL: {ex.Message}");
                    _uiService.Post(() =>
                    {
                        StatusText = $"Error installing mod: {ex.Message}";
                    });
                    return Models.Result<bool>.Fail(ex.Message, Models.ErrorCode.UnexpectedError);
                }
            });
        }
    }
}