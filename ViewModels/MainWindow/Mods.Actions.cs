namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowVM
    {
        private void ToggleMod(ModViewModel mod)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (mod != null)
                {
                    mod.IsEnabled = !mod.IsEnabled;
                    _modService.ToggleMod(mod.Name, mod.IsEnabled);

                    foreach (var group in Groups)
                    {
                        UpdateGroupStatus(group);
                    }

                    StatusText = $"{mod.Title} {(mod.IsEnabled ? "enabled" : "disabled")}";
                }
            });
        }

        private void RemoveMod(ModViewModel mod)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (mod != null)
                {
                    Mods.Remove(mod);
                    UpdateFilteredMods();
                    StatusText = $"Removed {mod.Title}";
                }
            });
        }
    }
}
