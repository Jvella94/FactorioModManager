using System.Threading.Tasks;
using FactorioModManager.Services;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowVM
    {
        private async Task OpenChangelogAsync()
        {
            if (SelectedMod == null) return;

            await Task.Run(async () =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    StatusText = "Fetching changelog...";
                });

                try
                {
                    var apiKey = _settingsService.GetApiKey();
                    var details = await _apiService.GetModDetailsAsync(SelectedMod.Name, apiKey);

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (details?.Changelog != null)
                        {
                            var window = new Views.ChangelogWindow(SelectedMod.Title, details.Changelog);
                            window.Show();
                            StatusText = "Changelog opened";
                        }
                        else
                        {
                            StatusText = "No changelog available";
                        }
                    });
                }
                catch (System.Exception ex)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Error fetching changelog: {ex.Message}";
                    });
                }
            });
        }

        private async Task OpenVersionHistoryAsync()
        {
            if (SelectedMod == null) return;

            await Task.Run(async () =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    StatusText = "Fetching version history...";
                });

                try
                {
                    var apiKey = _settingsService.GetApiKey();
                    var details = await _apiService.GetModDetailsAsync(SelectedMod.Name, apiKey);

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (details?.Releases != null && details.Releases.Count > 0)
                        {
                            var window = new Views.VersionHistoryWindow(SelectedMod.Title, details.Releases);
                            window.Show();
                            StatusText = "Version history opened";
                        }
                        else
                        {
                            StatusText = "No version history available";
                        }
                    });
                }
                catch (System.Exception ex)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Error fetching version history: {ex.Message}";
                    });
                }
            });
        }

        private async Task OpenSettingsAsync()
        {
            await Task.Run(() =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    var settingsWindow = new Views.SettingsWindow(_settingsService);

                    var owner = Avalonia.Application.Current?.ApplicationLifetime
                        is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow : null;

                    if (owner != null)
                    {
                        var result = await settingsWindow.ShowDialog<bool>(owner);

                        if (result)
                        {
                            StatusText = "Settings saved";
                        }
                    }
                    else
                    {
                        StatusText = "Unable to open settings window";
                    }
                });
            });
        }

    }
}
