using DynamicData;
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

            // ✅ DynamicData automatically updates FilteredMods and ModCountSummary
            SetStatus($"{mod.Title} {(mod.IsEnabled ? "enabled" : "disabled")}");
        }

        private void RemoveMod(ModViewModel? mod)
        {
            if (mod == null)
                return;

            // ✅ Remove from cache (DynamicData handles the rest)
            _modsCache.Remove(mod.Name);

            SetStatus($"Removed {mod.Title}");
        }
    }
}