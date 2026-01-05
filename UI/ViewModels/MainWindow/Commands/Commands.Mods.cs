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
        public ReactiveCommand<string, Unit> DeleteOldVersionCommand { get; private set; } = null!;

        // New: set active version command (accepts version string)
        public ReactiveCommand<string, Unit> SetActiveVersionCommand { get; private set; } = null!;

        public ReactiveCommand<Unit, Unit> CheckSingleModUpdateCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> RefreshSelectedModCommand { get; private set; } = null!;
        public ReactiveCommand<ModViewModel, Unit> ViewDependentsCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> LaunchFactorioCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> CheckForAppUpdatesCommand { get; private set; } = null!;

        // New command to update all mods that have pending updates
        public ReactiveCommand<Unit, Unit> UpdateAllCommand { get; private set; } = null!;

        private void InitializeModCommands()
        {
            RefreshModsCommand = ReactiveCommand.CreateFromTask(RefreshModsAsync);
            InstallModCommand = ReactiveCommand.CreateFromTask(InstallModAsync);
            OpenModFolderCommand = ReactiveCommand.Create(OpenModFolder);
            ToggleModCommand = ReactiveCommand.Create<ModViewModel>(ToggleMod);
            RemoveModCommand = ReactiveCommand.Create<ModViewModel>(RemoveMod);
            OpenModPortalCommand = ReactiveCommand.Create(OpenModPortal);
            OpenSourceUrlCommand = ReactiveCommand.Create(OpenSourceUrl);
            OpenChangelogCommand = ReactiveCommand.CreateFromTask(OpenChangelogAsync);
            OpenVersionHistoryCommand = ReactiveCommand.CreateFromTask(OpenVersionHistoryAsync);
            CheckUpdatesCustomCommand = ReactiveCommand.CreateFromTask(CheckUpdatesCustomAsync);
            DownloadUpdateCommand = ReactiveCommand.CreateFromTask<ModViewModel>(mod => UpdateSingleAsync(mod));

            // Changed: accept version string (CommandParameter="{Binding SelectedVersion}")
            DeleteOldVersionCommand = ReactiveCommand.Create<string>(DeleteOldVersion);

            // New: command to set selected version as active (async)
            SetActiveVersionCommand = ReactiveCommand.CreateFromTask<string>(SetActiveVersion);

            CheckSingleModUpdateCommand = ReactiveCommand.CreateFromTask(CheckSingleModUpdateAsync);
            RefreshSelectedModCommand = ReactiveCommand.CreateFromTask(RefreshSelectedModAsync);
            ViewDependentsCommand = ReactiveCommand.CreateFromTask<ModViewModel>(ViewDependentsAsync);
            LaunchFactorioCommand = ReactiveCommand.Create(LaunchFactorio);
            CheckForAppUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForAppUpdatesAsync);

            // Initialize new UpdateAllCommand
            UpdateAllCommand = ReactiveCommand.CreateFromTask(UpdateAllAsync);
        }

        /// <summary>
        /// Opens the mods folder in file explorer
        /// </summary>
        private void OpenModFolder()
        {
            try
            {
                var path = FolderPathHelper.GetModsDirectory();
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
                await CheckForUpdatesAsync(hours, isManual: true);
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

                if (installResult.Error != null)
                {
                    SetStatus(installResult.Error, LogLevel.Error);
                }
            }
        }

        /// <summary>
        /// Installs a mod from a local file
        /// </summary>
        private async Task<Result<bool>> InstallModFromFileAsync(string filePath)
        {
            try
            {
                var modInfo = _modService.ReadModInfo(filePath);
                if (modInfo == null || string.IsNullOrEmpty(modInfo.Name))
                {
                    await _uiService.InvokeAsync(() => SetStatus("Failed to read mod info from file", LogLevel.Error));
                    return Result<bool>.Fail("Invalid mod file: missing info.json", ErrorCode.InvalidModFormat);
                }

                var modName = modInfo.Name;
                var host = EnsureUpdatesHost();
                await host.SetStatusAsync($"Preparing to install {modName} from file...");

                // Ensure we have a host to orchestrate dependency resolution and installs
                // Delegate that performs the actual local-file installation and maps Result<bool> -> Result
                async Task<Result> InstallMainAsync()
                {
                    var installResult = await _downloadService.InstallFromLocalFileAsync(filePath);
                    if (installResult.Success)
                        return Result.Ok();
                    return Result.Fail(installResult.Error ?? "Install failed", installResult.Code);
                }

                var hostResult = await host.RunInstallWithDependenciesAsync(modName, InstallMainAsync);

                if (!hostResult.Success)
                {
                    // Host already sets informative status; mirror error in VM UI if needed
                    await host.SetStatusAsync(hostResult.Error ?? "Installation failed", LogLevel.Error);
                    return Result<bool>.Fail(hostResult.Error ?? "Installation failed", hostResult.Code);
                }

                // Refresh mods and select installed mod if present (host refreshed affected mods but do full refresh for VM state)
                await RefreshModsAsync();
                var installedMain = _allMods.FirstOrDefault(m => m.Name == modName);
                await _uiService.InvokeAsync(() =>
                {
                    if (installedMain != null)
                    {
                        SelectedMod = installedMain;
                    }
                });

                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                await _uiService.InvokeAsync(() => HandleError(ex, $"Error installing mod from file {filePath}"));
                return Result<bool>.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        /// <summary>
        /// Installs a mod from a mod portal URL
        /// </summary>
        private async Task<Result> InstallModFromUrlAsync(string url)
        {
            try
            {
                // Extract mod name robustly from URL (handle query strings and extra segments like /dependencies)
                string? modName = null;
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var segments = uri.Segments.Select(s => s.Trim('/')).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                    // Look for pattern: /mod/{modName}
                    for (int i = 0; i < segments.Length; i++)
                    {
                        if (segments[i].Equals("mod", StringComparison.OrdinalIgnoreCase) && i + 1 < segments.Length)
                        {
                            modName = segments[i + 1];
                            break;
                        }
                    }

                    // Fallback to last path segment if pattern not matched
                    if (string.IsNullOrEmpty(modName) && segments.Length > 0)
                        modName = segments.Last();
                }

                // Final fallback: naive split and trim query/fragment
                if (string.IsNullOrEmpty(modName))
                {
                    modName = url.Split('/').LastOrDefault();
                    if (!string.IsNullOrEmpty(modName))
                    {
                        var qIndex = modName.AsSpan().IndexOfAny(Constants.DependencyHelper.Markers);
                        if (qIndex >= 0)
                            modName = modName[..qIndex];
                    }
                }

                if (string.IsNullOrEmpty(modName))
                {
                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatus("Invalid mod portal URL", LogLevel.Error);
                    });
                    return Result.Fail("Invalid URL format", ErrorCode.InvalidInput);
                }

                var host = EnsureUpdatesHost();
                await host.SetStatusAsync($"Fetching {modName} from mod portal...");

                // Main install delegate uses existing InstallMod which downloads the latest release
                Task<Result> MainInstall() => InstallMod(modName);

                var hostResult = await host.RunInstallWithDependenciesAsync(modName, MainInstall);

                if (!hostResult.Success)
                {
                    await host.SetStatusAsync(hostResult.Error ?? "Installation failed", LogLevel.Error);
                    return hostResult;
                }

                // After host completed, refresh and select installed mod
                await RefreshModsAsync();
                var installedMain = _allMods.FirstOrDefault(m => m.Name == modName);
                await _uiService.InvokeAsync(() =>
                {
                    if (installedMain != null)
                        SelectedMod = installedMain;
                });

                return Result.Ok();
            }
            catch (Exception ex)
            {
                await _uiService.InvokeAsync(() =>
                {
                    HandleError(ex, $"Error installing mod from url {url}");
                });
                return Result.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        private async Task<Result> InstallMod(string modName)
        {
            try
            {
                var host = EnsureUpdatesHost();
                return await host.InstallModAsync(modName);
            }
            catch (Exception ex)
            {
                HandleError(ex, $"Error installing mod {modName}");
                return Result.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        /// <summary>
        /// Refreshes a single selected mod's info from the zip, refreshes installed versions, fetches portal metadata,
        /// reloads thumbnail and then checks for updates for that mod.
        /// Adds a confirmation dialog, uses the mod's download UI as a lightweight progress indicator,
        /// and logs telemetry-style events to the app log. Also recomputes and persists size on disk.
        /// </summary>
        private async Task RefreshSelectedModAsync()
        {
            if (SelectedMod == null)
            {
                SetStatus("No mod selected", LogLevel.Warning);
                return;
            }

            // Capture current selection to avoid races
            var selectedMod = SelectedMod;
            var modName = selectedMod.Name;
            var modTitle = selectedMod.Title;

            // Confirm action with the user
            var confirm = await _uiService.ShowConfirmationAsync(
                "Refresh Mod Info",
                $"Refresh all cached info for '{modTitle}' from the local mod file and portal? This will re-read info.json and refresh metadata.",
                null);
            if (!confirm)
            {
                _logService.LogDebug($"RefreshSelectedMod cancelled by user: {modName}");
                return;
            }

            // Telemetry / log - start
            _logService.Log($"Telemetry: RefreshSelectedMod Started for {modName}", LogLevel.Info);
            _logService.LogDebug($"Starting refresh of mod: {modName} ({selectedMod.FilePath})");

            await Task.Run(async () =>
            {
                // Use the mod's download UI as a transient progress indicator
                await _uiService.InvokeAsync(() =>
                {
                    selectedMod.IsDownloading = true;
                    selectedMod.HasDownloadProgress = false;
                    selectedMod.DownloadStatusText = "Refreshing mod info...";
                    SetStatus($"Refreshing info for {modTitle}...");
                });

                try
                {
                    var filePath = selectedMod.FilePath;
                    if (string.IsNullOrEmpty(filePath))
                    {
                        await _uiService.InvokeAsync(() =>
                        {
                            SetStatus($"Mod file path not available for {modTitle}", LogLevel.Warning);
                        });

                        _logService.LogWarning($"RefreshSelectedMod failed: file path missing for {modName}");
                        return;
                    }

                    // Read info.json from zip/directory
                    var modInfo = _modService.ReadModInfo(filePath);
                    if (modInfo == null)
                    {
                        await _uiService.InvokeAsync(() =>
                        {
                            SetStatus($"Failed to read info.json for {modTitle}", LogLevel.Error);
                        });

                        _logService.LogWarning($"RefreshSelectedMod: could not read info.json for {modName}");
                        return;
                    }

                    // Update UI-bound fields from local info.json
                    await _uiService.InvokeAsync(() =>
                    {
                        selectedMod.Title = string.IsNullOrEmpty(modInfo.Title) ? modInfo.Name : modInfo.Title;
                        selectedMod.Version = modInfo.Version;
                        selectedMod.Author = modInfo.Author;
                        selectedMod.Description = modInfo.Description ?? string.Empty;
                        selectedMod.Dependencies = modInfo.Dependencies;
                        selectedMod.FilePath = filePath;
                        selectedMod.DownloadStatusText = "Local info refreshed...";
                    });

                    // Refresh version cache and update available versions list
                    _modVersionManager.RefreshVersionCache(selectedMod.Name);
                    var installedVersions = _modVersionManager.GetInstalledVersions(selectedMod.Name);
                    await _uiService.InvokeAsync(() =>
                    {
                        selectedMod.AvailableVersions.Clear();
                        foreach (var v in installedVersions)
                        {
                            selectedMod.AvailableVersions.Add(v);
                        }
                        selectedMod.SelectedVersion = selectedMod.Version;
                        selectedMod.DownloadStatusText = "Versions refreshed...";
                    });

                    // Recompute size on disk (zip length or sum of directory files)
                    try
                    {
                        var size = ComputeSizeOnDisk(filePath);
                        _metadataService.UpdateSizeOnDisk(selectedMod.Name, size);
                        await _uiService.InvokeAsync(() =>
                        {
                            selectedMod.SizeOnDiskBytes = size;
                            selectedMod.DownloadStatusText = "Size refreshed...";
                        });

                        _logService.Log($"Telemetry: Size refreshed for {selectedMod.Name}: {size} bytes", LogLevel.Info);
                    }
                    catch (Exception exSize)
                    {
                        _logService.LogWarning($"Failed to compute size for {selectedMod.Name}: {exSize.Message}");
                    }

                    // Fetch full portal metadata (category, source url) and cache it
                    try
                    {
                        var details = await _apiService.GetModDetailsFullAsync(selectedMod.Name);
                        if (details != null)
                        {
                            _metadataService.UpdateAllPortalMetadata(selectedMod.Name, details.Category, details.SourceUrl);
                            await _uiService.InvokeAsync(() =>
                            {
                                selectedMod.Category = details.Category;
                                selectedMod.SourceUrl = details.SourceUrl;
                                selectedMod.DownloadStatusText = "Portal metadata refreshed...";
                            });

                            _logService.LogDebug($"RefreshSelectedMod: portal metadata updated for {selectedMod.Name}");
                        }
                        else
                        {
                            _logService.LogWarning($"RefreshSelectedMod: no portal details for {selectedMod.Name}");
                        }
                    }
                    catch (Exception exMeta)
                    {
                        _logService.LogWarning($"Failed to fetch portal metadata for {selectedMod.Name}: {exMeta.Message}");
                    }

                    // Reload thumbnail if available
                    await LoadThumbnailAsync(selectedMod);

                    // Finally, run the existing single-mod update check (will update metadata latest version/HasUpdate)
                    await CheckSingleModUpdateAsync();

                    // Telemetry / log - success
                    _logService.Log($"Telemetry: RefreshSelectedMod Completed for {selectedMod.Name}", LogLevel.Info);
                    _logService.LogDebug($"Completed refresh of mod: {selectedMod.Name}");
                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatus($"Refreshed {selectedMod.Title}");
                    });
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Telemetry: RefreshSelectedMod Failed for {selectedMod.Name}: {ex.Message}", ex);
                    HandleError(ex, $"Error refreshing mod info for {selectedMod.Title}");
                }
                finally
                {
                    // Clear progress indicator
                    await _uiService.InvokeAsync(() =>
                    {
                        selectedMod.IsDownloading = false;
                        selectedMod.HasDownloadProgress = false;
                        selectedMod.DownloadStatusText = string.Empty;
                    });
                }
            });
        }

        /// <summary>
        /// Launches Factorio using the configured executable path (with auto-detection fallback)
        /// </summary>
        private void LaunchFactorio()
        {
            try
            {
                // Prevent launching if Factorio is already running
                if (_factorioLauncher.IsFactorioRunning())
                {
                    SetStatus("Factorio is already running. Cannot launch another instance.", LogLevel.Warning);
                    _ = _uiService.ShowMessageAsync("Factorio is running", "Factorio appears to be already running. Please close it before launching from the manager.");
                    return;
                }

                var result = _factorioLauncher.Launch();
                if (result.Success)
                {
                    SetStatus("Launched Factorio");
                }
                else
                {
                    SetStatus(result.Error ?? "Failed to launch Factorio", LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                HandleError(ex, "Failed to launch Factorio");
            }
        }
    }
}