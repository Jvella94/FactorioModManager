using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowVM
    {
        private readonly ObservableCollection<ModViewModel> _navigationHistory = new();
        private int _navigationIndex = -1;

        public bool CanNavigateBack => _navigationIndex > 0;
        public bool CanNavigateForward => _navigationIndex < _navigationHistory.Count - 1;

        private void OnModSelected(ModViewModel? newMod)
        {
            if (newMod == null)
                return;

            // If current index points at same mod, do nothing
            if (_navigationIndex >= 0 &&
                _navigationIndex < _navigationHistory.Count &&
                _navigationHistory[_navigationIndex] == newMod)
            {
                return;
            }

            // Clear any forward history when navigating to a new mod
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
            var dependencyName = dependency.Split(DependencySeparators, System.StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(dependencyName)) return;

            var targetMod = Mods.FirstOrDefault(m =>
                m.Name.Equals(dependencyName, System.StringComparison.OrdinalIgnoreCase));

            if (targetMod != null)
            {
                SelectedMod = targetMod; // This will trigger OnModSelected
            }
        }
    }
}
