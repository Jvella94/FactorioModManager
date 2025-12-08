using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        private readonly ObservableCollection<ModViewModel> _navigationHistory = [];
        private int _navigationIndex = -1;

        public bool CanNavigateBack => _navigationIndex > 0;
        public bool CanNavigateForward => _navigationIndex < _navigationHistory.Count - 1;

        private void OnModSelected(ModViewModel? newMod)
        {
            if (newMod == null)
                return;

            if (_navigationIndex >= 0 &&
                _navigationIndex < _navigationHistory.Count &&
                _navigationHistory[_navigationIndex] == newMod)
            {
                return;
            }

            while (_navigationHistory.Count > _navigationIndex + 1)
            {
                _navigationHistory.RemoveAt(_navigationHistory.Count - 1);
            }

            _navigationHistory.Add(newMod);
            _navigationIndex = _navigationHistory.Count - 1;
            this.RaisePropertyChanged(nameof(CanNavigateBack));
            this.RaisePropertyChanged(nameof(CanNavigateForward));
        }

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

        private void NavigateToDependency(string dependency)
        {
            var dependencyName = dependency.Split(Constants.Separators.Dependency,
                StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(dependencyName))
                return;

            if (Constants.GameDependencies.IsGameDependency(dependencyName))
                return;

            var targetMod = Mods.FirstOrDefault(m =>
                m.Name.Equals(dependencyName, StringComparison.OrdinalIgnoreCase));

            if (targetMod != null)
            {
                SelectedMod = targetMod;
            }
            else
            {
                try
                {
                    var url = Constants.Urls.GetModUrl(dependencyName);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });

                    var isOptional = dependency.TrimStart().StartsWith('?') || dependency.Contains("(?)");
                    var depType = isOptional ? "optional" : "required";
                    StatusText = $"Opened mod portal for {depType} dependency: {dependencyName}";
                    _logService.Log($"Opened mod portal for {depType} dependency: {dependencyName}");
                }
                catch (Exception ex)
                {
                    StatusText = $"Error opening browser: {ex.Message}";
                    _logService.LogError($"Error opening mod portal: {ex.Message}");
                }
            }
        }
    }
}