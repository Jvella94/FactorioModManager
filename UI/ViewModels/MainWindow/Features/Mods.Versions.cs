using FactorioModManager.Models;
using FactorioModManager.Services;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        /// <summary>
        /// Loads all available versions for a mod
        /// </summary>
        private void LoadModVersions(ModViewModel mod)
        {
            try
            {
                var modsDirectory = FolderPathHelper.GetModsDirectory();
                var modFiles = Directory.GetFiles(modsDirectory, $"{mod.Name}_*.zip")
                    .OrderByDescending(f => f)
                    .ToList();

                mod.AvailableVersions.Clear();
                mod.VersionFilePaths.Clear();

                foreach (var file in modFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var parts = fileName.Split('_');

                    if (parts.Length >= 2)
                    {
                        var version = parts[^1]; // Last part is version
                        mod.AvailableVersions.Add(version);
                        mod.VersionFilePaths.Add(file);
                    }
                }

                // Set current version as selected
                mod.SelectedVersion = mod.Version;
                mod.InstalledCount = mod.AvailableVersions.Count;
                mod.RaisePropertyChanged(nameof(mod.HasMultipleVersions));
            }
            catch (Exception ex)
            {
                HandleError(ex, $"Error loading versions for {mod.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes an old version of a mod
        /// New signature: accepts the version string passed from the UI (SelectedVersion).
        /// Performs robust checks and retries, refreshes caches and UI state.
        /// </summary>
        internal void DeleteOldVersion(string? selectedVersion)
        {
            // Prefer the currently selected mod in the main UI
            var mod = SelectedMod;
            if (mod == null)
            {
                SetStatus("No mod selected", LogLevel.Warning);
                return;
            }

            if (string.IsNullOrEmpty(selectedVersion))
            {
                SetStatus("No version selected", LogLevel.Warning);
                return;
            }

            if (selectedVersion == mod.Version)
            {
                SetStatus("Cannot delete the currently active version", LogLevel.Warning);
                return;
            }

            try
            {
                // Ensure we have a fresh view of installed versions
                _modVersionManager.RefreshVersionCache(mod.Name);
                var installed = _modVersionManager.GetInstalledVersions(mod.Name);

                if (!installed.Contains(selectedVersion))
                {
                    SetStatus($"Version {selectedVersion} not found for {mod.Title}", LogLevel.Warning);
                    return;
                }

                // Build expected file path for helpful logging/messages
                var modsDirectory = FolderPathHelper.GetModsDirectory();
                var expectedFilePath = Path.Combine(modsDirectory, $"{mod.Name}_{selectedVersion}.zip");

                const int maxAttempts = 3;
                var attempt = 0;
                var deleted = false;
                Exception? lastEx = null;

                while (attempt < maxAttempts && !deleted)
                {
                    attempt++;
                    try
                    {
                        // Delegate deletion to version manager (central path handling + cache)
                        _modVersionManager.DeleteVersion(mod.Name, selectedVersion);
                        deleted = true;
                    }
                    catch (UnauthorizedAccessException uaEx)
                    {
                        // File is locked or permission denied — stop retrying, inform user
                        lastEx = uaEx;
                        _logService.LogWarning($"Delete attempt {attempt} failed due to access: {uaEx.Message}");
                        SetStatus($"Permission denied deleting {selectedVersion}. Close Factorio or other apps and try again.", LogLevel.Warning);
                        break;
                    }
                    catch (IOException ioEx)
                    {
                        // Transient IO errors (file locked momentarily). Retry a few times.
                        lastEx = ioEx;
                        _logService.LogWarning($"I/O error deleting version (attempt {attempt}) {expectedFilePath}: {ioEx.Message}");
                        if (attempt < maxAttempts)
                        {
                            Thread.Sleep(200 * attempt); // small backoff
                            continue;
                        }
                        SetStatus($"Failed to delete file: {ioEx.Message}", LogLevel.Warning);
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                        _logService.LogError($"Unexpected error deleting version {mod.Name}_{selectedVersion}: {ex.Message}", ex);
                        break;
                    }
                }

                if (!deleted)
                {
                    if (lastEx != null)
                        HandleError(lastEx, $"Error deleting old version: {lastEx.Message}");
                    // refresh UI state from disk to avoid showing stale options
                    LoadModVersions(mod);
                    return;
                }

                // Successful deletion: refresh caches and UI lists
                _modVersionManager.RefreshVersionCache(mod.Name);
                LoadModVersions(mod);

                SetStatus($"Deleted {mod.Title} version {selectedVersion}");
            }
            catch (Exception ex)
            {
                HandleError(ex, $"Error deleting old version: {ex.Message}");
            }
        }
    }
}