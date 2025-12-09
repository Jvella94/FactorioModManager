using FactorioModManager.Models;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;

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
                var modsDirectory = _modService.GetModsDirectory();
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
                _logService.LogError($"Error loading versions for {mod.Name}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deletes an old version of a mod
        /// </summary>
        internal void DeleteOldVersion(ModViewModel? mod)
        {
            if (mod == null || string.IsNullOrEmpty(mod.SelectedVersion))
            {
                SetStatus("No version selected", LogLevel.Warning);
                return;
            }

            if (mod.SelectedVersion == mod.Version)
            {
                SetStatus("Cannot delete the currently active version", LogLevel.Warning);
                return;
            }

            try
            {
                var versionIndex = mod.AvailableVersions.IndexOf(mod.SelectedVersion);
                if (versionIndex >= 0 && versionIndex < mod.VersionFilePaths.Count)
                {
                    var filePath = mod.VersionFilePaths[versionIndex];

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);

                        // Reload versions
                        LoadModVersions(mod);

                        SetStatus($"Deleted {mod.Title} version {mod.SelectedVersion}");
                    }
                    else
                    {
                        SetStatus($"File not found: {filePath}", LogLevel.Warning);
                    }
                }
                else
                {
                    SetStatus("Invalid version index", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error deleting old version: {ex.Message}", LogLevel.Error);
                _logService.LogError($"Delete version error: {ex.Message}", ex);
            }
        }
    }
}