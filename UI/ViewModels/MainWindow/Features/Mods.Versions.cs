using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        private void LoadModVersions(ModViewModel mod)
        {
            try
            {
                var modsDirectory = ModPathHelper.GetModsDirectory();
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

                mod.RaisePropertyChanged(nameof(mod.HasMultipleVersions));
            }
            catch (Exception ex)
            {
                _logService.LogDebug($"Error loading versions for {mod.Name}: {ex.Message}");
            }
        }

        internal void DeleteOldVersion(ModViewModel? mod) // CHANGED: private -> internal
        {
            if (mod == null || string.IsNullOrEmpty(mod.SelectedVersion))
                return;

            if (mod.SelectedVersion == mod.Version)
            {
                StatusText = "Cannot delete the currently active version";
                return;
            }

            _uiService.Post(() =>
            {
                try
                {
                    var versionIndex = mod.AvailableVersions.IndexOf(mod.SelectedVersion);
                    if (versionIndex >= 0 && versionIndex < mod.VersionFilePaths.Count)
                    {
                        var filePath = mod.VersionFilePaths[versionIndex];

                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            _logService.Log($"Deleted old version: {Path.GetFileName(filePath)}");

                            // Reload versions
                            LoadModVersions(mod);

                            StatusText = $"Deleted {mod.Title} version {mod.SelectedVersion}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    StatusText = $"Error deleting old version: {ex.Message}";
                    _logService.Log($"Error: {ex.Message}");
                }
            });
        }
    }
}
