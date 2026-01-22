using FactorioModManager.Models;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using static FactorioModManager.Constants;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        private readonly ObservableCollection<ModViewModel> _navigationHistory = [];
        private int _navigationIndex = -1;

        // Preview slot for showing a mod's details without selecting it in the list
        private ModViewModel? _previewMod;
        public ModViewModel? PreviewMod
        {
            get => _previewMod;
            set
            {
                var old = _previewMod;
                this.RaiseAndSetIfChanged(ref _previewMod, value);

                // If we set a new preview, ensure UI updates and navigation history records it
                if (value != null && old != value)
                {
                    try
                    {
                        // Add to navigation history so back/forward works for previews as well
                        OnModSelected(value);

                        // Load thumbnail asynchronously (best-effort)
                        _ = LoadThumbnailAsync(value);

                        // Populate SourceUrl from metadata cache if available
                        try { value.SourceUrl = _metadataService.GetSourceUrl(value.Name); } catch { }

                        // Populate InstalledDependencies tuples (so dependency status in preview is correct)
                        try
                        {
                            var installedDeps = new System.Collections.Generic.List<(string Name, string? InstalledVersion)>();
                            foreach (var raw in value.Dependencies)
                            {
                                var name = DependencyHelper.ExtractDependencyName(raw);
                                if (string.IsNullOrEmpty(name)) continue;
                                var ver = _modVersionManager?.GetInstalledVersions(name).FirstOrDefault();
                                if (!string.IsNullOrEmpty(ver)) installedDeps.Add((name, ver));
                            }
                            value.InstalledDependencies = installedDeps;
                        }
                        catch { }
                    }
                    catch { }
                }

                // Notify DetailMod binding may change
                this.RaisePropertyChanged(nameof(DetailMod));
                this.RaisePropertyChanged(nameof(HasDetailMod));
            }
        }

        public bool CanNavigateBack => _navigationIndex > 0;
        public bool CanNavigateForward => _navigationIndex < _navigationHistory.Count - 1;

        /// <summary>
        /// Handles mod selection and adds to navigation history
        /// </summary>
        private void OnModSelected(ModViewModel? newMod)
        {
            if (newMod == null)
                return;

            // Don't add duplicate consecutive entries
            if (_navigationIndex >= 0 &&
                _navigationIndex < _navigationHistory.Count &&
                _navigationHistory[_navigationIndex] == newMod)
            {
                return;
            }

            // Clear forward history when navigating to a new mod
            while (_navigationHistory.Count > _navigationIndex + 1)
            {
                _navigationHistory.RemoveAt(_navigationHistory.Count - 1);
            }

            _navigationHistory.Add(newMod);
            _navigationIndex = _navigationHistory.Count - 1;

            const int MaxHistorySize = 50;
            if (_navigationHistory.Count > MaxHistorySize)
            {
                _navigationHistory.RemoveAt(0);
                _navigationIndex--;
            }

            this.RaisePropertyChanged(nameof(CanNavigateBack));
            this.RaisePropertyChanged(nameof(CanNavigateForward));
        }

        /// <summary>
        /// Navigates to the previous mod in history
        /// </summary>
        private void NavigateBack()
        {
            if (!CanNavigateBack)
                return;

            _navigationIndex--;
            var mod = _navigationHistory[_navigationIndex];
            // If the mod is visible in the filtered list, select it, otherwise show as preview without modifying history
            if (_filteredMods.Contains(mod))
            {
                SelectedMod = mod;
                SetStatus($"Navigated to {mod.Title}");
            }
            else
            {
                // Assign backing field directly to avoid OnModSelected being called again
                _previewMod = mod;
                this.RaisePropertyChanged(nameof(PreviewMod));
                this.RaisePropertyChanged(nameof(DetailMod));
                this.RaisePropertyChanged(nameof(HasDetailMod));
                SetStatus($"Previewing {mod.Title} (not in current filter)");
            }

            this.RaisePropertyChanged(nameof(CanNavigateBack));
            this.RaisePropertyChanged(nameof(CanNavigateForward));
        }

        /// <summary>
        /// Navigates to the next mod in history
        /// </summary>
        private void NavigateForward()
        {
            if (!CanNavigateForward)
                return;

            _navigationIndex++;
            var mod = _navigationHistory[_navigationIndex];
            // If the mod is visible in the filtered list, select it, otherwise show as preview without modifying history
            if (_filteredMods.Contains(mod))
            {
                SelectedMod = mod;
                SetStatus($"Navigated to {mod.Title}");
            }
            else
            {
                // Assign backing field directly to avoid OnModSelected being called again
                _previewMod = mod;
                this.RaisePropertyChanged(nameof(PreviewMod));
                this.RaisePropertyChanged(nameof(DetailMod));
                this.RaisePropertyChanged(nameof(HasDetailMod));
                SetStatus($"Previewing {mod.Title} (not in current filter)");
            }

            this.RaisePropertyChanged(nameof(CanNavigateBack));
            this.RaisePropertyChanged(nameof(CanNavigateForward));
        }

        /// <summary>
        /// Navigates to a dependency mod or opens mod portal if not installed
        /// </summary>
        private void NavigateToDependency(string dependency)
        {
            var dependencyName = DependencyHelper.ExtractDependencyName(dependency);
            if (string.IsNullOrEmpty(dependencyName))
            {
                SetStatus("Invalid dependency", LogLevel.Warning);
                return;
            }

            if (DependencyHelper.IsGameDependency(dependencyName))
            {
                SetStatus($"{dependencyName} is a base game dependency");
                return;
            }

            var targetMod = _allMods.FirstOrDefault(m => m.Name.Equals(dependencyName, StringComparison.OrdinalIgnoreCase));
            if (targetMod != null)
            {
                // If target is visible in the current filtered list, select it normally.
                if (_filteredMods.Contains(targetMod))
                {
                    SelectedMod = targetMod;
                    SetStatus($"Navigated to {targetMod.Title}");
                }
                else
                {
                    // Otherwise, show a non-invasive preview without changing list selection
                    PreviewMod = targetMod;
                    SetStatus($"Previewing {targetMod.Title} (not in current filter)");
                }
            }
            else
            {
                OpenDependencyInPortal(dependencyName, dependency);
            }
        }

        /// <summary>
        /// Opens a dependency in the mod portal
        /// </summary>
        private void OpenDependencyInPortal(string dependencyName, string dependency)
        {
            try
            {
                var url = Urls.GetModUrl(dependencyName);
                _uiService.OpenUrl(url);
                var isOptional = DependencyHelper.IsOptionalDependency(dependency);
                var depType = isOptional ? "optional" : "required";
                SetStatus($"Opened mod portal for {depType} dependency: {dependencyName}");
            }
            catch (Exception ex)
            {
                HandleError(ex, $"Error opening portal dependency: {ex.Message}");
            }
        }
    }
}