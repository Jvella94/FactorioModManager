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

            // ✅ ADD LIMIT
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
            SelectedMod = mod;

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
            SelectedMod = mod;

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

            // Skip game dependencies
            if (DependencyHelper.IsGameDependency(dependencyName))
            {
                SetStatus($"{dependencyName} is a base game dependency");
                return;
            }

            // Try to find installed mod
            var targetMod = _modsCache.Items.FirstOrDefault(m => m.Name.Equals(dependencyName, StringComparison.OrdinalIgnoreCase));

            if (targetMod != null)
            {
                // Navigate to installed mod
                SelectedMod = targetMod;
                SetStatus($"Navigated to {targetMod.Title}");
            }
            else
            {
                // Open mod portal for uninstalled dependency
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
                SetStatus($"Error opening browser: {ex.Message}", LogLevel.Error);
                _logService.LogError($"Error opening mod portal: {ex.Message}", ex);
            }
        }
    }
}