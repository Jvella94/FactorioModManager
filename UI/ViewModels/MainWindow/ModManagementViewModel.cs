using ReactiveUI;
using System;
using System.Collections.ObjectModel;

namespace FactorioModManager.ViewModels.MainWindow
{
    public class ModManagementViewModel : ReactiveObject
    {
        private readonly ObservableCollection<ModViewModel> _allMods = [];
        private readonly ObservableCollection<ModViewModel> _filteredMods = [];

        public ObservableCollection<ModViewModel> FilteredMods => _filteredMods;

        public void ApplyModFilter(string searchText)
        {
            _filteredMods.Clear();
            foreach (var mod in _allMods)
            {
                if (mod.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    _filteredMods.Add(mod);
                }
            }
        }
    }
}