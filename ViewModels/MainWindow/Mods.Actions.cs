namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowVM
    {
        private void ToggleMod(ModViewModel? mod)
        {
            if (mod == null) return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Toggle the enabled state
                mod.IsEnabled = !mod.IsEnabled;

                // Persist to mod-list.json
                _modService.ToggleMod(mod.Name, mod.IsEnabled);

                // Update group statuses
                foreach (var group in Groups)
                {
                    if (group.ModNames.Contains(mod.Title))
                    {
                        UpdateGroupStatus(group);
                    }
                }

                // Refresh filtered mods if "Show Disabled" is off
                if (!ShowDisabled)
                {
                    UpdateFilteredMods();
                }

                StatusText = $"{mod.Title} {(mod.IsEnabled ? "enabled" : "disabled")}";
            });
        }

        private void RemoveMod(ModViewModel? mod)
        {
            if (mod == null) return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Mods.Remove(mod);
                UpdateFilteredMods();
                StatusText = $"Removed {mod.Title}";
            });
        }
    }
}
