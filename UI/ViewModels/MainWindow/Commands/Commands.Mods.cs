using FactorioModManager.Models;
using FactorioModManager.Services;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Threading;
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
            DownloadUpdateCommand = ReactiveCommand.CreateFromTask<ModViewModel>(DownloadUpdateAsync);

            // Changed: accept version string (CommandParameter="{Binding SelectedVersion}")
            DeleteOldVersionCommand = ReactiveCommand.Create<string>(DeleteOldVersion);

            // New: command to set selected version as active (async)
            SetActiveVersionCommand = ReactiveCommand.CreateFromTask<string>(SetActiveVersion);

            CheckSingleModUpdateCommand = ReactiveCommand.CreateFromTask(CheckSingleModUpdateAsync);
            RefreshSelectedModCommand = ReactiveCommand.CreateFromTask(RefreshSelectedModAsync);
            ViewDependentsCommand = ReactiveCommand.CreateFromTask<ModViewModel>(ViewDependentsAsync);
            LaunchFactorioCommand = ReactiveCommand.Create(LaunchFactorio);
            CheckForAppUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForAppUpdatesCustomAsync);

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
        private async Task<Models.Result<bool>> InstallModFromFileAsync(string filePath)
        {
            return await _downloadService.InstallFromLocalFileAsync(filePath);
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

                await _uiService.InvokeAsync(() =>
                {
                    SetStatus($"Fetching {modName} from mod portal...");
                });

                var resolution = await _dependencyFlow.ResolveForInstallAsync(modName, _allMods);
                if (!resolution.Proceed)
                    return Result.Fail("Installation cancelled by user due to dependencies.", ErrorCode.OperationCancelled);

                // If there are missing dependencies to install, include them in the overall progress
                var startedBatch = false;
                if (!IsDownloadProgressVisible)
                {
                    startedBatch = true;
                    var totalToInstall = resolution.MissingDependenciesToInstall.Count + 1; // deps + main
                    await _uiService.InvokeAsync(() =>
                    {
                        // reset internal counters and show UI
                        Interlocked.Exchange(ref _downloadProgressCompleted, 0);
                        DownloadProgressTotal = totalToInstall;
                        DownloadProgressCompleted = 0;
                        IsDownloadProgressVisible = true;
                    });
                }

                await _uiService.InvokeAsync(() =>
                {
                    // apply enable/disable decisions
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

                try
                {
                    foreach (var mod in resolution.MissingDependenciesToInstall)
                    {
                        var result = await InstallMod(mod);
                        if (!result.Success)
                        {
                            await _uiService.InvokeAsync(() =>
                            {
                                SetStatus($"Failed to install dependency {modName}: {result.Error}", LogLevel.Warning);
                            });
                            return result;
                        }
                        // Increment batch progress for each dependency installed
                        if (IsDownloadProgressVisible)
                        {
                            var newVal = System.Threading.Interlocked.Increment(ref _downloadProgressCompleted);
                            await _uiService.InvokeAsync(() => DownloadProgressCompleted = newVal);
                        }
                    }

                    await InstallMod(modName);
                    if (IsDownloadProgressVisible)
                    {
                        var newVal = System.Threading.Interlocked.Increment(ref _downloadProgressCompleted);
                        await _uiService.InvokeAsync(() => DownloadProgressCompleted = newVal);
                    }
                    await RefreshModsAsync();
                    var installedMain = _allMods.FirstOrDefault(m => m.Name == modName);
                    await _uiService.InvokeAsync(() =>
                    {
                        if (installedMain != null)
                        {
                            installedMain.IsEnabled = resolution.InstallEnabled;

                            _modService.ToggleMod(installedMain.Name, resolution.InstallEnabled);

                            SelectedMod = installedMain;
                            SetStatus(
                                resolution.InstallEnabled
                                    ? $"Successfully installed and enabled {installedMain.Title}."
                                    : $"Successfully installed {installedMain.Title} (disabled).");
                        }
                        else
                        {
                            SetStatus($"Successfully downloaded {modName}, but it was not found after refresh.", LogLevel.Warning);
                        }
                    });
                }
                finally
                {
                    if (startedBatch)
                    {
                        try
                        {
                            // ensure counters cleared and UI hidden
                            Interlocked.Exchange(ref _downloadProgressCompleted, 0);
                            await _uiService.InvokeAsync(() =>
                            {
                                DownloadProgressCompleted = 0;
                                DownloadProgressTotal = 0;
                                try { DownloadProgress.UpdateSpeedText(null); } catch { }
                                try { DownloadProgress.UpdateProgressPercent(0.0); } catch { }
                                IsDownloadProgressVisible = false;
                            });
                        }
                        catch
                        {
                            // best-effort
                            Interlocked.Exchange(ref _downloadProgressCompleted, 0);
                            DownloadProgressCompleted = 0;
                            DownloadProgressTotal = 0;
                            try { DownloadProgress.UpdateSpeedText(null); } catch { }
                            try { DownloadProgress.UpdateProgressPercent(0.0); } catch { }
                            IsDownloadProgressVisible = false;
                        }
                    }
                }

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
            // If not already running batch update UI, use the global Download progress UI for single installs
            var singleProgress = false;
            if (!IsDownloadProgressVisible)
            {
                singleProgress = true;
                await _uiService.InvokeAsync(() =>
                {
                    DownloadProgressTotal = 1;
                    DownloadProgressCompleted = 0;
                    IsDownloadProgressVisible = true;
                });
            }

            try
            {
                var modDetails = await _apiService.GetModDetailsAsync(modName);
                if (modDetails?.Releases == null || modDetails.Releases.Count == 0)
                {
                    _logService.LogWarning($"Failed to fetch release details for {modName}");
                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatus($"Failed to fetch mod details for {modName}", LogLevel.Error);
                    });
                    return Result.Fail("No release information found", ErrorCode.ApiRequestFailed);
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
                    return Result.Fail("No download URL", ErrorCode.ApiRequestFailed);
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
                    latestRelease.DownloadUrl);

                if (!downloadResult.Success)
                    return downloadResult;

                // At this point the mod zip exists locally; get its ModInfo
                var modsDirectory = FolderPathHelper.GetModsDirectory();
                var downloadedPath = System.IO.Path.Combine(
                    modsDirectory,
                    $"{modName}_{latestRelease.Version}.zip");

                var modInfo = _modService.ReadModInfo(downloadedPath);
                if (modInfo == null)
                {
                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatus($"Installed {modTitle}, but could not read info.json for dependency checks.", LogLevel.Warning);
                    });
                    return downloadResult;
                }

                return Result.Ok();
            }
            finally
            {
                if (singleProgress)
                {
                    // mark complete and clear UI
                    await _uiService.InvokeAsync(() =>
                    {
                        DownloadProgressCompleted = 1;
                        IsDownloadProgressVisible = false;
                        DownloadProgressTotal = 0;
                        DownloadProgressCompleted = 0;
                        try { DownloadProgress.UpdateSpeedText(null); } catch { }
                        try { DownloadProgress.UpdateProgressPercent(0.0); } catch { }
                    });
                }
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

        private async Task CheckForAppUpdatesCustomAsync()
        {
            await CheckForAppUpdatesAsync();
        }
    }
}