using ReactiveUI;
using System.Linq;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        private void ToggleMod(ModViewModel? mod)
        {
            if (mod == null)
                return;

            mod.IsEnabled = !mod.IsEnabled;
            _modService.ToggleMod(mod.Name, mod.IsEnabled);

            // Update groups
            foreach (var group in Groups.Where(g => g.ModNames.Contains(mod.Title)))
            {
                UpdateGroupStatus(group);
            }

            this.RaisePropertyChanged(nameof(ModCountSummary));
            ApplyModFilter(); // Reapply filter if needed
            SetStatus($"{mod.Title} {(mod.IsEnabled ? "enabled" : "disabled")}");
        }

        private void RemoveMod(ModViewModel? mod)
        {
            if (mod == null)
                return;

            _allMods.Remove(mod);
            ApplyModFilter(); // Reapply filter
            SetStatus($"Removed {mod.Title}");
        }
    }
}